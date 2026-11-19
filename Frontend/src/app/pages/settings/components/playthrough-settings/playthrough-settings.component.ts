import { TranslationKey } from '../../../../i18n/translations';
import { TranslatePipe } from '../../../../i18n/translate.pipe';
import { LocaleService, LocalizedMessage } from '../../../../i18n/locale.service';
import { GameArtComponent } from '../../../../components/game-art/game-art.component';
import { Component, OnInit, inject } from '@angular/core';
import { CommonModule, DOCUMENT } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { SettingsService } from '../../../../service/settings.service';
import {
  Playthrough,
  PlaythroughGameOption,
  PlaythroughSettings,
  PlaythroughStatus,
  PlaythroughStreamCategory,
  UpsertPlaythroughRequest
} from '../../../../models/playthrough.model';

interface PlaythroughStatusOption {
  value: PlaythroughStatus;
  label: TranslationKey;
}

interface PlaythroughFormState {
  /** null while creating a new playthrough. */
  id: string | null;
  twitchCategoryId: string;
  title: string;
  status: PlaythroughStatus;
  autoAddNewStreams: boolean;
  streamCategoryIds: string[];
}

type StatusFilter = 'All' | PlaythroughStatus;

/** The game search only appears once the list is too long to scan. */
const GAME_SEARCH_THRESHOLD = 8;

@Component({
  selector: 'app-playthrough-settings',
  standalone: true,
  imports: [TranslatePipe, GameArtComponent, CommonModule, FormsModule],
  templateUrl: './playthrough-settings.component.html',
  styleUrls: ['./playthrough-settings.component.scss']
})
export class PlaythroughSettingsComponent implements OnInit {
  readonly locale = inject(LocaleService);
  private readonly document = inject(DOCUMENT);

  readonly statusOptions: PlaythroughStatusOption[] = [
    { value: 'Playing', label: 'ui.playing' },
    { value: 'Planned', label: 'ui.willPlay' },
    { value: 'Completed', label: 'ui.completed' },
    { value: 'Dropped', label: 'ui.dropped' }
  ];

  settings: PlaythroughSettings = { playthroughs: [], availableGames: [] };
  /** The open editor; null when nothing is being created or edited. */
  form: PlaythroughFormState | null = null;
  statusFilter: StatusFilter = 'All';
  gameQuery = '';
  isLoading = false;
  loadFailed = false;
  isSaving = false;
  /** Playthrough whose delete confirmation is shown in its row. */
  confirmingDeleteId: string | null = null;
  /** Playthrough with a quick status change or deletion in flight. */
  busyId: string | null = null;
  notice: { text: LocalizedMessage; error: boolean } | null = null;
  formError: LocalizedMessage = '';

  constructor(private readonly settingsService: SettingsService) {}

  ngOnInit(): void {
    this.loadSettings(true);
  }

  get isCreating(): boolean {
    return this.form !== null && this.form.id === null;
  }

  get suggestedGames(): PlaythroughGameOption[] {
    return this.settings.availableGames.filter(game => game.suggested);
  }

  get showGameSearch(): boolean {
    return this.settings.availableGames.length > GAME_SEARCH_THRESHOLD;
  }

  get filteredGames(): PlaythroughGameOption[] {
    const query = this.gameQuery.trim().toLocaleLowerCase();
    const games = this.settings.availableGames;
    return query ? games.filter(game => game.name.toLocaleLowerCase().includes(query)) : games;
  }

  /** Statuses that actually have playthroughs, so no tab ever opens an empty list. */
  get statusTabs(): { value: StatusFilter; label: TranslationKey; count: number }[] {
    const tabs = this.statusOptions
      .map(option => ({
        value: option.value as StatusFilter,
        label: option.label,
        count: this.settings.playthroughs.filter(playthrough => playthrough.status === option.value).length
      }))
      .filter(tab => tab.count > 0);
    return tabs.length > 1
      ? [{ value: 'All', label: 'ui.all', count: this.settings.playthroughs.length }, ...tabs]
      : [];
  }

  get visiblePlaythroughs(): Playthrough[] {
    return this.statusFilter === 'All'
      ? this.settings.playthroughs
      : this.settings.playthroughs.filter(playthrough => playthrough.status === this.statusFilter);
  }

  /** Game shown in the editor: free streams plus, when editing, the ones already in the playthrough. */
  get editorGame(): PlaythroughGameOption | undefined {
    if (!this.form) return undefined;
    const form = this.form;
    const availableGame = this.settings.availableGames.find(game => game.twitchCategoryId === form.twitchCategoryId);
    const editing = form.id ? this.settings.playthroughs.find(playthrough => playthrough.id === form.id) : undefined;
    if (!editing) return availableGame;

    const streamCategories = [...editing.streamCategories];
    availableGame?.streamCategories.forEach(segment => {
      if (!streamCategories.some(item => item.streamCategoryId === segment.streamCategoryId)) streamCategories.push(segment);
    });
    streamCategories.sort((left, right) =>
      new Date(right.categoryStartedAt).getTime() - new Date(left.categoryStartedAt).getTime());

    return {
      twitchCategoryId: editing.twitchCategoryId,
      twitchCategoryTwitchId: editing.twitchCategoryTwitchId,
      name: editing.gameName,
      boxArtUrl: editing.boxArtUrl,
      streamCategories
    };
  }

  /** Only one playthrough per game may collect new streams. */
  get autoAddConflict(): boolean {
    const form = this.form;
    return !!form && this.hasAutoAddPlaythrough(form.twitchCategoryId, form.id);
  }

  get autoAddAvailable(): boolean {
    return !!this.form && this.form.status !== 'Completed' && !this.autoAddConflict;
  }

  get allStreamsSelected(): boolean {
    const total = this.editorGame?.streamCategories.length ?? 0;
    return total > 0 && this.form?.streamCategoryIds.length === total;
  }

  loadSettings(initial = false): void {
    this.isLoading = initial;
    this.loadFailed = false;

    this.settingsService.getPlaythroughs().subscribe({
      next: (settings) => {
        this.settings = settings;
        this.isLoading = false;
        if (this.statusFilter !== 'All' && !this.visiblePlaythroughs.length) this.statusFilter = 'All';
      },
      error: (error) => {
        console.error('Failed to load playthrough settings', error);
        this.loadFailed = initial;
        this.notice = { text: 'settings.loadPlaythroughsFailed', error: true };
        this.isLoading = false;
      }
    });
  }

  /** Opens the editor for a new playthrough; a given game comes with all of its free streams selected. */
  startCreate(game?: PlaythroughGameOption): void {
    this.form = { id: null, twitchCategoryId: '', title: '', status: 'Playing', autoAddNewStreams: false, streamCategoryIds: [] };
    this.gameQuery = '';
    this.resetTransientState();
    const onlyGame = this.settings.availableGames.length === 1 ? this.settings.availableGames[0] : undefined;
    if (game ?? onlyGame) this.chooseGame((game ?? onlyGame)!);
    this.revealEditor();
  }

  chooseGame(game: PlaythroughGameOption): void {
    if (!this.form) return;
    this.form.twitchCategoryId = game.twitchCategoryId;
    this.form.streamCategoryIds = game.streamCategories.map(segment => segment.streamCategoryId);
    this.form.autoAddNewStreams = !this.hasAutoAddPlaythrough(game.twitchCategoryId, null);
    this.formError = '';
  }

  /** Back to the game list while creating. */
  clearGame(): void {
    if (!this.form) return;
    this.form.twitchCategoryId = '';
    this.form.streamCategoryIds = [];
  }

  startEdit(playthrough: Playthrough): void {
    this.form = {
      id: playthrough.id,
      twitchCategoryId: playthrough.twitchCategoryId,
      title: playthrough.title,
      status: playthrough.status,
      autoAddNewStreams: playthrough.autoAddNewStreams,
      streamCategoryIds: playthrough.streamCategories.map(category => category.streamCategoryId)
    };
    this.resetTransientState();
    this.revealEditor();
  }

  cancelEdit(): void {
    this.form = null;
    this.formError = '';
  }

  save(): void {
    const form = this.form;
    if (!form || this.isSaving) return;
    if (!form.twitchCategoryId) {
      this.formError = 'settings.selectGameError';
      return;
    }

    this.isSaving = true;
    this.formError = '';
    this.notice = null;

    const request: UpsertPlaythroughRequest = {
      id: form.id,
      twitchCategoryId: form.twitchCategoryId,
      title: form.title,
      status: form.status,
      autoAddNewStreams: this.autoAddAvailable && form.autoAddNewStreams,
      streamCategoryIds: form.streamCategoryIds
    };

    this.settingsService.savePlaythrough(request).subscribe({
      next: () => {
        this.isSaving = false;
        this.notice = { text: form.id ? 'settings.playthroughUpdated' : 'settings.playthroughCreated', error: false };
        this.form = null;
        this.loadSettings();
      },
      error: (error) => {
        console.error('Failed to save playthrough', error);
        this.formError = 'settings.savePlaythroughFailed';
        this.isSaving = false;
      }
    });
  }

  /** Status is the most frequent change, so it is saved straight from the list. */
  changeStatus(playthrough: Playthrough, select: HTMLSelectElement): void {
    const status = select.value as PlaythroughStatus;
    if (status === playthrough.status || this.busyId) return;
    this.busyId = playthrough.id;
    this.notice = null;

    this.settingsService.savePlaythrough({
      id: playthrough.id,
      twitchCategoryId: playthrough.twitchCategoryId,
      title: playthrough.title,
      status,
      autoAddNewStreams: status !== 'Completed' && playthrough.autoAddNewStreams,
      streamCategoryIds: playthrough.streamCategories.map(category => category.streamCategoryId)
    }).subscribe({
      next: () => {
        this.busyId = null;
        this.notice = {
          text: { key: 'pt.statusUpdated', params: { name: playthrough.gameName, status: this.statusLabel(status) } },
          error: false
        };
        this.loadSettings();
      },
      error: (error) => {
        console.error('Failed to change playthrough status', error);
        this.busyId = null;
        this.notice = { text: 'settings.savePlaythroughFailed', error: true };
        select.value = playthrough.status;
      }
    });
  }

  askDelete(playthrough: Playthrough): void {
    this.confirmingDeleteId = playthrough.id;
    this.notice = null;
  }

  delete(playthrough: Playthrough): void {
    if (this.busyId) return;
    this.busyId = playthrough.id;

    this.settingsService.deletePlaythrough(playthrough.id).subscribe({
      next: () => {
        this.busyId = null;
        this.confirmingDeleteId = null;
        if (this.form?.id === playthrough.id) this.form = null;
        this.notice = { text: 'settings.playthroughDeleted', error: false };
        this.loadSettings();
      },
      error: (error) => {
        console.error('Failed to delete playthrough', error);
        this.busyId = null;
        this.confirmingDeleteId = null;
        this.notice = { text: 'settings.deletePlaythroughFailed', error: true };
      }
    });
  }

  toggleStreamCategory(streamCategoryId: string, checked: boolean): void {
    if (!this.form) return;
    const ids = this.form.streamCategoryIds.filter(id => id !== streamCategoryId);
    this.form.streamCategoryIds = checked ? [...ids, streamCategoryId] : ids;
  }

  toggleAllStreams(): void {
    if (!this.form) return;
    this.form.streamCategoryIds = this.allStreamsSelected
      ? []
      : (this.editorGame?.streamCategories ?? []).map(segment => segment.streamCategoryId);
  }

  isSelected(streamCategoryId: string): boolean {
    return !!this.form?.streamCategoryIds.includes(streamCategoryId);
  }

  /** A game can appear on one stream more than once, so streams are counted, not segments. */
  streamCount(streamCategories: PlaythroughStreamCategory[]): number {
    return new Set(streamCategories.map(segment => segment.streamSessionId)).size;
  }

  /** First and last stream of the playthrough: the shelf shows games, not custom titles. */
  playthroughPeriod(playthrough: Playthrough): string {
    const dates = playthrough.streamCategories.map(segment => new Date(segment.streamStartedAt).getTime()).filter(Number.isFinite);
    if (!dates.length) return '';
    const format = (value: number) => new Intl.DateTimeFormat(this.locale.locale(), { dateStyle: 'medium', timeZone: 'UTC' }).format(new Date(value));
    const first = Math.min(...dates);
    const last = Math.max(...dates);
    return format(first) === format(last) ? format(first) : `${format(first)} – ${format(last)}`;
  }

  statusLabel(status: PlaythroughStatus): string {
    return this.locale.t(this.statusOptions.find(option => option.value === status)?.label || 'ui.unknown');
  }

  formatStreamCategoryDate(value: string): string {
    return new Intl.DateTimeFormat(this.locale.locale(), { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
  }

  private hasAutoAddPlaythrough(twitchCategoryId: string, exceptId: string | null): boolean {
    return this.settings.playthroughs.some(playthrough =>
      playthrough.twitchCategoryId === twitchCategoryId && playthrough.id !== exceptId && playthrough.autoAddNewStreams);
  }

  private resetTransientState(): void {
    this.formError = '';
    this.notice = null;
    this.confirmingDeleteId = null;
  }

  /** The editor opens in place (top of the page or inside a row), so bring it into view and focus it. */
  private revealEditor(): void {
    setTimeout(() => {
      const editor = this.document.getElementById('playthrough-editor');
      editor?.scrollIntoView({ block: 'nearest' });
      editor?.focus({ preventScroll: true });
    });
  }
}
