import { Component, OnInit } from '@angular/core';
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
  label: string;
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
  imports: [CommonModule, FormsModule],
  templateUrl: './playthrough-settings.component.html',
  styleUrls: ['./playthrough-settings.component.scss']
})
export class PlaythroughSettingsComponent implements OnInit {
  private readonly streamCategoryDateFormatter = new Intl.DateTimeFormat(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short'
  });

  readonly statusOptions: PlaythroughStatusOption[] = [
    { value: 'Playing', label: 'Playing' },
    { value: 'Planned', label: 'Will Play' },
    { value: 'Dropped', label: 'Dropped' },
    { value: 'Completed', label: 'Completed' }
  ];

  settings: PlaythroughSettings = { playthroughs: [], availableGames: [] };
  form: PlaythroughFormState = this.createDefaultForm();
  isLoading = false;
  isSaving = false;
  errorMessage = '';
  successMessage = '';

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
        this.errorMessage = 'Failed to load playthrough settings.';
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
      this.errorMessage = 'Select a game.';
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
        this.successMessage = this.form.id ? 'Playthrough updated.' : 'Playthrough created.';
        this.form = this.createDefaultForm();
        this.loadSettings();
      },
      error: (error) => {
        console.error('Failed to save playthrough', error);
        this.errorMessage = this.extractError(error, 'Failed to save playthrough.');
        this.isSaving = false;
      }
    });
  }

  delete(playthrough: Playthrough): void {
    if (!confirm(`Delete "${playthrough.title}"?`)) {
      return;
    }

    this.errorMessage = '';
    this.successMessage = '';

    this.settingsService.deletePlaythrough(playthrough.id).subscribe({
      next: () => {
        if (this.form.id === playthrough.id) {
          this.form = this.createDefaultForm();
        }

        this.successMessage = 'Playthrough deleted.';
        this.loadSettings();
      },
      error: (error) => {
        console.error('Failed to delete playthrough', error);
        this.errorMessage = this.extractError(error, 'Failed to delete playthrough.');
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

  formatStreamCategoryDate(value: string): string {
    return this.streamCategoryDateFormatter.format(new Date(value));
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

  private extractError(error: unknown, fallback: string): string {
    const httpError = error as { error?: { errors?: unknown; data?: { error?: string } } | string };

    if (typeof httpError?.error === 'string') {
      return httpError.error;
    }

    if (httpError?.error && typeof httpError.error === 'object') {
      const apiError = httpError.error as { data?: { error?: string } };
      if (apiError.data?.error) {
        return apiError.data.error;
      }
    }

    return fallback;
  }
}
