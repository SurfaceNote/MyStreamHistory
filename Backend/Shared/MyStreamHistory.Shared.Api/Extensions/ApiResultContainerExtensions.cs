using Microsoft.AspNetCore.Mvc;
using MyStreamHistory.Shared.Api.Mapping;
using MyStreamHistory.Shared.Api.Wrappers;
using System.Runtime.CompilerServices;

namespace MyStreamHistory.Shared.Api.Extensions
{
    public static class ApiResultContainerExtensions
    {
        public static ActionResult<ApiResultContainer<T>> Success<T>(this ControllerBase controller, T data)
        {
            return new OkObjectResult(new ApiResultContainer<T>
            {
                Success = true,
                Data = data
            });
        }

        public static ActionResult<ApiResultContainer> Success(this ControllerBase controller)
        {
            return new OkObjectResult(new ApiResultContainer
            {
                Success = true
            });
        }

        public static ActionResult<ApiResultContainer<T>> Fail<T>(this ControllerBase controller, params string[] errors)
        {
            var statusCode = ErrorStatusMapper.GetPriorityStatusCode(errors);

            return new ObjectResult(new ApiResultContainer<T>
            {
                Success = false,
                Errors = errors.ToList()
            })
            {
                StatusCode = statusCode
            };
        }

        public static ActionResult<ApiResultContainer> Fail(this ControllerBase controller, params string[] errors)
        {
            var statusCode = ErrorStatusMapper.GetPriorityStatusCode(errors);

            return new ObjectResult(new ApiResultContainer
            {
                Success = false,
                Errors = errors.ToList()
            })
            {
                StatusCode = statusCode
            };
        }
    }
}
