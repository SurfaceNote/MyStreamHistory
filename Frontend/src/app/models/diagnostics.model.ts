export interface EventSubSubscriptions {
  data: EventSubSubscription[];
  total: number;
  totalCost: number;
  maxTotalCost: number;
}

export interface EventSubSubscription {
  id: string;
  type: string;
  version: string;
  status: string;
  cost: number;
  condition: {
    broadcasterUserId: string;
  };
  transport: {
    method: string;
    callback: string;
  };
  createdAt: string;
}

export interface ChatSubscriptionsResponse {
  count: number;
  subscriptions: ChatSubscription[];
}

export interface ChatSubscription {
  id: string;
  status: string;
  broadcasterUserId: string;
  createdAt: string;
}

export interface DeleteSubscriptionsResponse {
  deletedCount: number;
  message: string;
}

export interface CleanupChatSubscriptionsResponse {
  deletedCount: number;
  message: string;
}

export interface SubscribeToAllUsersResponse {
  userCount: number;
  successCount: number;
  failCount: number;
  message: string;
}
