export type PlaythroughStatus = 'Playing' | 'Planned' | 'Dropped' | 'Completed';

export interface PlaythroughStreamCategory {
  streamCategoryId: string;
  streamSessionId: string;
  twitchCategoryId: string;
  gameName: string;
  streamTitle: string;
  streamStartedAt: string;
  streamEndedAt?: string | null;
  categoryStartedAt: string;
  categoryEndedAt?: string | null;
}

export interface Playthrough {
  id: string;
  twitchCategoryId: string;
  twitchCategoryTwitchId: string;
  gameName: string;
  boxArtUrl: string;
  title: string;
  status: PlaythroughStatus;
  autoAddNewStreams: boolean;
  createdAt: string;
  updatedAt: string;
  streamCategories: PlaythroughStreamCategory[];
}

export interface PlaythroughGameOption {
  twitchCategoryId: string;
  twitchCategoryTwitchId: string;
  name: string;
  boxArtUrl: string;
  streamCategories: PlaythroughStreamCategory[];
}

export interface PlaythroughSettings {
  playthroughs: Playthrough[];
  availableGames: PlaythroughGameOption[];
}

export interface UpsertPlaythroughRequest {
  id?: string | null;
  twitchCategoryId: string;
  title: string;
  status: PlaythroughStatus;
  autoAddNewStreams: boolean;
  streamCategoryIds: string[];
}
