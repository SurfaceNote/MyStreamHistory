import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
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
  imports: [CommonModule, FormsModule],
  templateUrl: './admin.component.html',
  styleUrl: './admin.component.scss'
})
export class AdminComponent implements OnInit {
  readonly deleteEventSubConfirmation = 'delete-all-eventsub-subscriptions';
  readonly cleanupChatConfirmation = 'cleanup-all-chat-subscriptions';

  eventSubSubscriptions: EventSubSubscriptions | null = null;
  chatSubscriptions: ChatSubscriptionsResponse | null = null;
  isAdmin = false;
  loading = false;
  actionInProgress: AdminAction | null = null;
  errorMessage = '';
  successMessage = '';
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
      this.successMessage = result.message || `Created subscriptions for ${result.successCount} of ${result.userCount} users.`;
      this.refreshDiagnostics();
    });
  }

  deleteAllEventSubSubscriptions(): void {
    if (this.deleteEventSubInput !== this.deleteEventSubConfirmation) {
      this.errorMessage = 'Enter the exact EventSub confirmation phrase before deleting.';
      return;
    }

    this.runAction('deleteEventSub', () => this.diagnosticsService.deleteAllEventSubSubscriptions(), (result) => {
      this.successMessage = result.message || `Deleted ${result.deletedCount} EventSub subscriptions.`;
      this.deleteEventSubInput = '';
      this.refreshDiagnostics();
    });
  }

  cleanupChatSubscriptions(): void {
    if (this.cleanupChatInput !== this.cleanupChatConfirmation) {
      this.errorMessage = 'Enter the exact chat confirmation phrase before cleanup.';
      return;
    }

    this.runAction('cleanupChat', () => this.diagnosticsService.cleanupChatSubscriptions(), (result) => {
      this.successMessage = result.message || `Cleaned up ${result.deletedCount} chat subscriptions.`;
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

  private getErrorMessage(error: unknown): string {
    const httpError = error as { status?: number; error?: { message?: string }; message?: string };

    if (httpError.status === 401) {
      return 'You need to sign in before opening diagnostics.';
    }

    if (httpError.status === 403) {
      return 'Diagnostics are available only for admin accounts, and production access must be enabled on the backend.';
    }

    if (httpError.status === 429) {
      return 'Diagnostics rate limit reached. Wait a moment and try again.';
    }

    return httpError.error?.message ?? httpError.message ?? 'Diagnostics request failed.';
  }
}
