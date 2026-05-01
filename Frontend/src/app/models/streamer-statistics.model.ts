import { CategoryStatistics } from './category-statistics.model';
import { PlaythroughStatistics } from './playthrough-statistics.model';

export interface StreamerStatistics {
    totalStreamsCount: number;
    totalUniqueGamesCount: number;
    totalStreamedHours: number;
    allGamesTotalHours: number;
    categories: CategoryStatistics[];
    playthroughs: PlaythroughStatistics[];
    dashboard: StreamerDashboard;
}

export type StreamerDashboardPeriod = '7d' | '30d' | '90d' | 'all';

export interface TimeSeriesPoint {
    date: string;
    value: number;
}

export interface CategoryAnalytics {
    twitchCategoryId: string;
    twitchId: string;
    name: string;
    boxArtUrl: string;
    totalHours: number;
    streamsCount: number;
    averageViewers: number;
}

export interface StreamerDashboard {
    period: StreamerDashboardPeriod;
    from?: string;
    to?: string;
    totalStreamsCount: number;
    totalUniqueGamesCount: number;
    totalStreamedHours: number;
    streamedHoursByDay: TimeSeriesPoint[];
    streamedHoursByWeek: TimeSeriesPoint[];
    chatPointsByDay: TimeSeriesPoint[];
    viewerCountByDay: TimeSeriesPoint[];
    topCategories: CategoryAnalytics[];
}
