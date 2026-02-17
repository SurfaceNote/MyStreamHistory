import { CategoryStatistics } from './category-statistics.model';

export interface StreamerStatistics {
    totalStreamsCount: number;
    totalUniqueGamesCount: number;
    totalStreamedHours: number;
    categories: CategoryStatistics[];
}

