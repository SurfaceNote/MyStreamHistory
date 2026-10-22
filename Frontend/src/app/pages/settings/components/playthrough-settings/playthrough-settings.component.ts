import { TranslationKey } from '../../../../i18n/translations';
import { TranslatePipe } from '../../../../i18n/translate.pipe';
import { LocaleService, LocalizedMessage } from '../../../../i18n/locale.service';
import { GameArtComponent } from '../../../../components/game-art/game-art.component';
import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { SettingsService } from '../../../../service/settings.service';
import {
  Playthrough,
  PlaythroughGameOption,
  PlaythroughSettings,
  PlaythroughStatus,
  UpsertPlaythroughRequest
} from '../../../../models/playthrough.model';

interface PlaythroughStatusOption {
  value: PlaythroughStatus;
  label: TranslationKey;
}

interface PlaythroughFormState {
  id: string | null;
  twitchCategoryId: string;
  title: string;
  status: PlaythroughStatus;
  autoAddNewStreams: boolean;
  streamCategoryIds: string[];
}

@Component({
  selector: 'app-playthrough-settings',
  standalone: true,
  imports: [TranslatePipe, GameArtComponent, CommonModule, FormsModule],
  templateUrl: './playthrough-settings.component.html',
  styleUrls: ['./playthrough-settings.component.scss']
})
export class PlaythroughSettingsComponent implements OnInit {
  readonly locale = inject(LocaleService);

  readonly statusOptions: PlaythroughStatusOption[] = [
    { value: 'Playing', label: 'ui.playing' },
    { value: 'Planned', label: 'ui.willPlay' },
    { value: 'Dropped', label: 'ui.dropped' },
    { value: 'Completed', label: 'ui.completed' }
  ];

  settings: PlaythroughSettings = { playthroughs: [], availableGames: [] };
  form: PlaythroughFormState = this.createDefaultForm();
  isLoading = false;
  isSaving = false;
  private _errorMessage: LocalizedMessage = '';
  get errorMessage(): string { return this.locale.message(this._errorMessage); }
  set errorMessage(value: LocalizedMessage) { this._errorMessage = value; }
  private _successMessage: LocalizedMessage = '';
  get successMessage(): string { return this.locale.message(this._successMessage); }
  set successMessage(value: LocalizedMessage) { this._successMessage = value; }

  constructor(private readonly settingsService: SettingsService) {}

  ngOnInit(): void {
    this.loadSettings();
  }

  get selectedGameImageUrl(): string {
    return this.formatBoxArtUrl(this.selectedGame?.boxArtUrl);
  }

  get selectedGame(): PlaythroughGameOption | undefined {
    const availableGame = this.settings.availableGames.find(game => game.twitchCategoryId === this.form.twitchCategoryId);
    const editingPlaythrough = this.form.id
      ? this.settings.playthroughs.find(playthrough => playthrough.id === this.form.id)
      : undefined;

    if (!editingPlaythrough) {
      return availableGame;
    }

    const assignedSegments = editingPlaythrough.streamCategories;

    if (!availableGame) {
      return {
        twitchCategoryId: editingPlaythrough.twitchCategoryId,
        twitchCategoryTwitchId: editingPlaythrough.twitchCategoryTwitchId,
        name: editingPlaythrough.gameName,
        boxArtUrl: editingPlaythrough.boxArtUrl,
        streamCategories: assignedSegments
      };
    }

    const mergedStreamCategories = [...availableGame.streamCategories];

    assignedSegments.forEach(segment => {
      if (!mergedStreamCategories.some(item => item.streamCategoryId === segment.streamCategoryId)) {
        mergedStreamCategories.push(segment);
      }
    });

    mergedStreamCategories.sort((left, right) =>
      new Date(right.categoryStartedAt).getTime() - new Date(left.categoryStartedAt).getTime());

    return {
      ...availableGame,
      streamCategories: mergedStreamCategories
    };
  }

  get gameOptions(): PlaythroughGameOption[] {
    if (!this.form.id) {
      return this.settings.availableGames;
    }

    const editingPlaythrough = this.settings.playthroughs.find(playthrough => playthrough.id === this.form.id);
    if (!editingPlaythrough) {
      return this.settings.availableGames;
    }

    const gameExists = this.settings.availableGames.some(game => game.twitchCategoryId === editingPlaythrough.twitchCategoryId);
    if (gameExists) {
      return this.settings.availableGames;
    }

    return [
      {
        twitchCategoryId: editingPlaythrough.twitchCategoryId,
        twitchCategoryTwitchId: editingPlaythrough.twitchCategoryTwitchId,
        name: editingPlaythrough.gameName,
        boxArtUrl: editingPlaythrough.boxArtUrl,
        streamCategories: editingPlaythrough.streamCategories
      },
      ...this.settings.availableGames
    ];
  }

  loadSettings(): void {
    this.isLoading = true;
    this.errorMessage = '';

    this.settingsService.getPlaythroughs().subscribe({
      next: (settings) => {
        this.settings = {
          playthroughs: settings.playthroughs.map(playthrough => ({
            ...playthrough,
            boxArtUrl: this.formatBoxArtUrl(playthrough.boxArtUrl)
          })),
          availableGames: settings.availableGames.map(game => ({
            ...game,
            boxArtUrl: this.formatBoxArtUrl(game.boxArtUrl)
          }))
        };
        this.isLoading = false;

        if (this.form.id === null && !this.form.twitchCategoryId && this.settings.availableGames.length > 0) {
          this.form.twitchCategoryId = this.settings.availableGames[0].twitchCategoryId;
        }
      },
      error: (error) => {
        console.error('Failed to load playthrough settings', error);
        this.errorMessage = 'settings.loadPlaythroughsFailed';
        this.isLoading = false;
      }
    });
  }

  startCreate(): void {
    this.form = this.createDefaultForm();
    this.form.twitchCategoryId = this.settings.availableGames[0]?.twitchCategoryId ?? '';
    this.errorMessage = '';
    this.successMessage = '';
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

    this.errorMessage = '';
    this.successMessage = '';
  }

  save(): void {
    if (!this.form.twitchCategoryId) {
      this.errorMessage = 'settings.selectGameError';
      return;
    }

    this.isSaving = true;
    this.errorMessage = '';
    this.successMessage = '';

    const request: UpsertPlaythroughRequest = {
      id: this.form.id,
      twitchCategoryId: this.form.twitchCategoryId,
      title: this.form.title,
      status: this.form.status,
      autoAddNewStreams: this.form.autoAddNewStreams,
      streamCategoryIds: this.form.streamCategoryIds
    };

    this.settingsService.savePlaythrough(request).subscribe({
      next: () => {
        this.isSaving = false;
        this.successMessage = this.form.id ? 'settings.playthroughUpdated' : 'settings.playthroughCreated';
        this.form = this.createDefaultForm();
        this.loadSettings();
      },
      error: (error) => {
        console.error('Failed to save playthrough', error);
        this.errorMessage = 'settings.savePlaythroughFailed';
        this.isSaving = false;
      }
    });
  }

  delete(playthrough: Playthrough): void {
    if (!confirm(this.locale.t('settings.deletePlaythroughConfirm', { name: playthrough.title || playthrough.gameName }))) {
      return;
    }

    this.errorMessage = '';
    this.successMessage = '';

    this.settingsService.deletePlaythrough(playthrough.id).subscribe({
      next: () => {
        if (this.form.id === playthrough.id) {
          this.form = this.createDefaultForm();
        }

        this.successMessage = 'settings.playthroughDeleted';
        this.loadSettings();
      },
      error: (error) => {
        console.error('Failed to delete playthrough', error);
        this.errorMessage = 'settings.deletePlaythroughFailed';
      }
    });
  }

  toggleStreamCategory(streamCategoryId: string, checked: boolean): void {
    if (checked) {
      if (!this.form.streamCategoryIds.includes(streamCategoryId)) {
        this.form.streamCategoryIds = [...this.form.streamCategoryIds, streamCategoryId];
      }
      return;
    }

    this.form.streamCategoryIds = this.form.streamCategoryIds.filter(id => id !== streamCategoryId);
  }

  isSelected(streamCategoryId: string): boolean {
    return this.form.streamCategoryIds.includes(streamCategoryId);
  }

  trackByPlaythrough(_: number, playthrough: Playthrough): string {
    return playthrough.id;
  }

  trackByGame(_: number, game: PlaythroughGameOption): string {
    return game.twitchCategoryId;
  }

  statusLabel(status: PlaythroughStatus): string {
    return this.locale.t(this.statusOptions.find(option => option.value === status)?.label || 'ui.unknown');
  }

  formatStreamCategoryDate(value: string): string {
    return new Intl.DateTimeFormat(this.locale.locale(), { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
  }

  private formatBoxArtUrl(boxArtUrl: string | undefined): string {
    if (!boxArtUrl) {
      return '';
    }

    return boxArtUrl
      .replace('{width}', '144')
      .replace('{height}', '192');
  }

  private createDefaultForm(): PlaythroughFormState {
    return {
      id: null,
      twitchCategoryId: '',
      title: '',
      status: 'Playing',
      autoAddNewStreams: false,
      streamCategoryIds: []
    };
  }

}
