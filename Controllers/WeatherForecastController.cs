using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace ETagExample.Controllers
{
    public static class EtagHelper
    {
        public static readonly IActionResult NotModifiedResult = new StatusCodeResult(304);

        public static bool CanUseCachedVersion(string etag, HttpContext httpContext)
        {
            bool isGetRequest = httpContext.Request.Method == "GET";
            bool hasEtag = httpContext.Request.Headers.TryGetValue(HeaderNames.IfNoneMatch, out StringValues etagFromRequest);
            if (etag == etagFromRequest && hasEtag && isGetRequest)
            {
                return true;
            }

            httpContext.Response.Headers.Add(HeaderNames.ETag, new[] { etag });
            return false;
        }
    }

    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private static readonly IReadOnlyList<WeatherForecast> db = new List<WeatherForecast>()
        {
            new WeatherForecast()
            {
                Date = DateTime.Parse("01/01/2022 07:00:00"),
                LastModifiedUtc = DateTime.Parse("01/01/2022 07:00:00"),
                Summary = "summary 1",
                TemperatureC = -12
            },
            new WeatherForecast()
            {
                Date = DateTime.Parse("02/01/2022 07:00:00"),
                LastModifiedUtc = DateTime.Parse("01/02/2022 08:00:00"),
                Summary = "summary 2",
                TemperatureC = -11
            }
        };

        [HttpGet]
        [Route("{id}")]
        public async Task<IActionResult> Get([FromRoute] int id)
        {
            WeatherForecast entity;
            try
            {
                entity = db[id]; // In the real project it should be very fast query by index
            }
            catch (ArgumentOutOfRangeException)
            {
                return NotFound();
            }

            // For this example I decided to use date as etag to demonstrate that etag can be anything
            if (EtagHelper.CanUseCachedVersion(entity.LastModifiedUtc.ToString(), HttpContext))  
            {
                return EtagHelper.NotModifiedResult;
            }

            await Task.Delay(1000); // Some long running task

            return Ok(entity);
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var lastUpdatedEntity = db.Max(e => e.LastModifiedUtc);
            if (EtagHelper.CanUseCachedVersion(lastUpdatedEntity.ToString(), HttpContext))
            {
                return EtagHelper.NotModifiedResult;
            }

            await Task.Delay(1000); // Some long running task

            return Ok(db);
        }

        [HttpPut]
        [Route("{id}")]
        public async Task<IActionResult> UpdateLastModifiedDate([FromRoute] int id)
        {
            WeatherForecast entityToUpdate;
            try
            {
                entityToUpdate = db[id];
            }
            catch (ArgumentOutOfRangeException)
            {
                return NotFound();
            }

            entityToUpdate.LastModifiedUtc = DateTime.UtcNow; // Some update operation
            await Task.Delay(1000); 

            return Ok();
        }
    }
}

