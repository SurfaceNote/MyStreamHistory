export interface ApiResponse<T> {
    success: boolean;
    data: T,
    errors: unknown | null;
    meta?: {
        timestamp: string;
        correlationId: string | null;
    };
}