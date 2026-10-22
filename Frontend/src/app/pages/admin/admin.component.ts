import { TranslatePipe } from '../../i18n/translate.pipe';
import { LocaleService, LocalizedMessage } from '../../i18n/locale.service';
import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { forkJoin, Observable } from 'rxjs';
import { AuthService } from '../../auth/auth.service';
import {
  ChatSubscriptionsResponse,
  CleanupChatSubscriptionsResponse,
  DeleteSubscriptionsResponse,
  EventSubSubscriptions,
  SubscribeToAllUsersResponse
} from '../../models/diagnostics.model';
import { DiagnosticsService } from '../../service/diagnostics.service';

type AdminAction = 'refresh' | 'subscribeAll' | 'deleteEventSub' | 'cleanupChat';

@Component({
  selector: 'app-admin',
  standalone: true,
  imports: [TranslatePipe, CommonModule, FormsModule, RouterLink],
  templateUrl: './admin.component.html',
  styleUrl: './admin.component.scss'
})
export class AdminComponent implements OnInit {
  readonly locale = inject(LocaleService);
  readonly deleteEventSubConfirmation = 'delete-all-eventsub-subscriptions';
  readonly cleanupChatConfirmation = 'cleanup-all-chat-subscriptions';

  eventSubSubscriptions: EventSubSubscriptions | null = null;
  chatSubscriptions: ChatSubscriptionsResponse | null = null;
  isAdmin = false;
  loading = false;
  actionInProgress: AdminAction | null = null;
  private _errorMessage: LocalizedMessage = '';
  get errorMessage(): string { return this.locale.message(this._errorMessage); }
  set errorMessage(value: LocalizedMessage) { this._errorMessage = value; }
  private _successMessage: LocalizedMessage = '';
  get successMessage(): string { return this.locale.message(this._successMessage); }
  set successMessage(value: LocalizedMessage) { this._successMessage = value; }
  deleteEventSubInput = '';
  cleanupChatInput = '';

  constructor(
    private authService: AuthService,
    private diagnosticsService: DiagnosticsService
  ) {}

  ngOnInit(): void {
    this.isAdmin = this.authService.isAdmin();

    if (this.isAdmin) {
      this.refreshDiagnostics();
    }
  }

  refreshDiagnostics(): void {
    this.loading = true;
    this.actionInProgress = 'refresh';
    this.errorMessage = '';

    forkJoin({
      eventSub: this.diagnosticsService.getEventSubSubscriptions(),
      chat: this.diagnosticsService.getChatSubscriptions()
    }).subscribe({
      next: ({ eventSub, chat }) => {
        this.eventSubSubscriptions = eventSub;
        this.chatSubscriptions = chat;
        this.loading = false;
        this.actionInProgress = null;
      },
      error: (error) => this.handleError(error)
    });
  }

  subscribeToAllUsers(): void {
    this.runAction('subscribeAll', () => this.diagnosticsService.subscribeToAllUsers(), (result) => {
      this.successMessage = { key: 'admin.subscribed', params: { count: result.successCount, total: result.userCount } };
      this.refreshDiagnostics();
    });
  }

  deleteAllEventSubSubscriptions(): void {
    if (this.deleteEventSubInput !== this.deleteEventSubConfirmation) {
      this.errorMessage = 'admin.confirmEventSub';
      return;
    }

    this.runAction('deleteEventSub', () => this.diagnosticsService.deleteAllEventSubSubscriptions(), (result) => {
      this.successMessage = { key: 'admin.deletedEventSub', params: { count: result.deletedCount } };
      this.deleteEventSubInput = '';
      this.refreshDiagnostics();
    });
  }

  cleanupChatSubscriptions(): void {
    if (this.cleanupChatInput !== this.cleanupChatConfirmation) {
      this.errorMessage = 'admin.confirmChat';
      return;
    }

    this.runAction('cleanupChat', () => this.diagnosticsService.cleanupChatSubscriptions(), (result) => {
      this.successMessage = { key: 'admin.cleanedChat', params: { count: result.deletedCount } };
      this.cleanupChatInput = '';
      this.refreshDiagnostics();
    });
  }

  isActionRunning(action: AdminAction): boolean {
    return this.actionInProgress === action;
  }

  private runAction<T>(
    action: AdminAction,
    request: () => Observable<T>,
    onSuccess: (result: T) => void
  ): void {
    this.actionInProgress = action;
    this.errorMessage = '';
    this.successMessage = '';

    request().subscribe({
      next: (result: T) => {
        this.actionInProgress = null;
        onSuccess(result);
      },
      error: (error: unknown) => this.handleError(error)
    });
  }

  private handleError(error: unknown): void {
    this.loading = false;
    this.actionInProgress = null;
    this.successMessage = '';
    this.errorMessage = this.getErrorMessage(error);
  }

  private getErrorMessage(error: unknown): LocalizedMessage {
    const httpError = error as { status?: number; error?: { message?: string }; message?: string };

    if (httpError.status === 401) {
      return 'admin.signInRequired';
    }

    if (httpError.status === 403) {
      return 'admin.forbidden';
    }

    if (httpError.status === 429) {
      return 'admin.rateLimited';
    }

    return 'admin.failed';
  }
}
