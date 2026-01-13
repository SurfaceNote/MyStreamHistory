export class ApiResponseError extends Error {
    constructor(
        message: string,
        public readonly errors: unknown | null,
        public readonly meta?: { timestamp: string; correlationId: string | null }
    ) {
        super(message);
        this.name = 'ApiResponseError';
    }
}