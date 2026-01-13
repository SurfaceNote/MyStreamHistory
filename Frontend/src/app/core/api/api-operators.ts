import { map } from "rxjs";
import { ApiResponseError } from "./api-error";
import { ApiResponse } from "./api-response.model";

export function unwrapData<T>() {
    return map((res: ApiResponse<T>) => {
        if (!res?.success) {
            throw new ApiResponseError('API response success = false', res?.errors ?? null, res?.meta);
        }
        return res.data;
    })
}