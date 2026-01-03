using MyStreamHistory.Shared.Base.Error;

namespace MyStreamHistory.Shared.Api.Mapping
{
    public class ErrorStatusMapper
    {
        private static readonly Dictionary<string, int> CodeToStatus = new()
        {
            [ErrorCodes.NotFound] = 404,
            [ErrorCodes.AlreadyExist] = 409,
            [ErrorCodes.PermissionDenied] = 403,
            [ErrorCodes.InvalidCredentials] = 401,
            [ErrorCodes.RegistrationTimeout] = 504,
            [ErrorCodes.InternalError] = 500,
        };

        private static readonly int[] Priority = { 500, 504, 403, 401, 409, 400, 422 };
        
        public static int GetStatusCode(string error)
        {
            if (string.IsNullOrEmpty(error))
            {
                return 422;
            }

            return CodeToStatus.TryGetValue(error, out var status) ? status : 422;
        }

        public static int GetPriorityStatusCode(IEnumerable<string>? errors)
        {
            if (errors == null || !errors.Any())
            {
                return 422;
            }

            var statusCodes = errors
                .Select(code => CodeToStatus.TryGetValue(code, out var status) ? status : 422)
                .Distinct()
                .ToList();

            foreach (var p in Priority)
            {
                if (statusCodes.Contains(p))
                {
                    return p;
                }
            }

            return 422;
        }
    }
}
