import { CategoryStatistics } from './category-statistics.model';
import { PlaythroughStatistics } from './playthrough-statistics.model';

export interface StreamerStatistics {
    totalStreamsCount: number;
    totalUniqueGamesCount: number;
    totalStreamedHours: number;
    allGamesTotalHours: number;
    categories: CategoryStatistics[];
    playthroughs: PlaythroughStatistics[];
}
