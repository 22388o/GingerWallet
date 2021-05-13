using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using System.Collections.Generic;
using System.IO;
using System.Text;
using WalletWasabi.Backend.Filters;
using Xunit;

namespace WalletWasabi.Tests.UnitTests.WabiSabi.Backend.Filters
{
	public class IdempotencyActionFilterTests
	{
		[Fact]
		public void ShouldCacheResponse()
		{
			var helloBody = Encoding.UTF8.GetBytes("Hello world");
			using var stream1 = new MemoryStream(helloBody);
			var actionContext = CreateActionExecutingContext(stream1);
			var cache = new MemoryCache(new MemoryCacheOptions());
			var filter = new IdempotentActionFilterAttributeImpl(cache);

			filter.OnActionExecuting(actionContext);

			var resultContext = CreateResulExecutedContext(actionContext);
			filter.OnResultExecuted(resultContext);

			var byeBody = Encoding.UTF8.GetBytes("Bye world");
			using var stream2 = new MemoryStream(byeBody);
			actionContext = CreateActionExecutingContext(stream2);
			filter.OnActionExecuting(actionContext);

			Assert.Equal(1, cache.Count);

			var returned = Assert.IsType<OkObjectResult>(resultContext.Result);
			var objValue = Assert.IsType<KeyValuePair<string, string>>(returned.Value);
			Assert.Equal("Greeting", objValue.Key);
			Assert.Equal("Hello world", objValue.Value);
		}

		private static ActionExecutingContext CreateActionExecutingContext(Stream bodyStream)
		{
			var modelState = new ModelStateDictionary();
			var httpContext = new DefaultHttpContext();
			httpContext.Request.Path = "/api";
			httpContext.Request.Body = bodyStream;
			var actionContext = new ActionExecutingContext(
				new ActionContext(
					httpContext: httpContext,
					routeData: new RouteData(),
					actionDescriptor: new ActionDescriptor(),
					modelState: modelState),
				new List<IFilterMetadata>(),
				new Dictionary<string, object>(),
				new Mock<Controller>().Object);

			return actionContext;
		}

		private static ResultExecutedContext CreateResulExecutedContext(ActionExecutingContext actionContext)
		{
			return new ResultExecutedContext(
				actionContext,
				actionContext.Filters,
				new OkObjectResult(new KeyValuePair<string, string>("Greeting", "Hello world")),
				actionContext.Controller);
		}
	}
}