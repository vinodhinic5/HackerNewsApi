using System.Text.Json;
using HackerNewsApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace HackerNewsApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StoriesController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _client;
    private readonly IMemoryCache _cache;

    public StoriesController(IHttpClientFactory httpClientFactory, IMemoryCache cache)
    {
        _client = httpClientFactory.CreateClient("HackerNews");
        _cache = cache;
    }

    [HttpGet("best")] //The Actual/Final Get API created
    public async Task<ActionResult<List<Story>>> GetBest([FromQuery] int n)
    {
        if (n <= 0)
            return BadRequest("n must be a positive number");

        var ids = await GetBestIds();
        var stories = await GetStories(ids);

        var best = stories.OrderByDescending(s => s.Score).Take(n).ToList();
        return Ok(best);
    }

    /// <summary>
    /// Method to get the BestIds
    /// </summary>
    /// <returns></returns>
    private async Task<List<int>> GetBestIds()
    {
        if (_cache.TryGetValue("bestIds", out List<int>? ids) && ids != null)
            return ids;

        ids = await _client.GetFromJsonAsync<List<int>>("beststories.json") ?? new List<int>();
        _cache.Set("bestIds", ids, TimeSpan.FromMinutes(1));

        return ids;
    }

    /// <summary>
    /// Method to get the stories for the ids
    /// </summary>
    /// <param name="ids"></param>
    /// <returns></returns>
    private async Task<List<Story>> GetStories(List<int> ids)
    {
        // Only allow 20 requests to Hacker News at once, so we don't hammer their API.
        using var gate = new SemaphoreSlim(20);

        var tasks = ids.Select(id => GetStory(id, gate));
        var results = await Task.WhenAll(tasks);

        return results.Where(s => s != null).Select(s => s!).ToList();
    }
    
    /// <summary>
    /// Get the story for each Id
    /// </summary>
    /// <param name="id"></param>
    /// <param name="gate"></param>
    /// <returns></returns>
    private async Task<Story?> GetStory(int id, SemaphoreSlim gate)
    {
        var cacheKey = $"story:{id}";
        if (_cache.TryGetValue(cacheKey, out Story? cached))
            return cached;

        await gate.WaitAsync();
        HnItem? item;
        try
        {
            item = await _client.GetFromJsonAsync<HnItem>($"item/{id}.json", JsonOptions);
        }
        finally
        {
            gate.Release();
        }

        if (item == null || item.Deleted || item.Dead)
            return null;

        var story = new Story
        {
            Title = item.Title ?? "",
            Uri = string.IsNullOrWhiteSpace(item.Url) ? $"https://news.ycombinator.com/item?id={id}" : item.Url,
            PostedBy = item.By ?? "",
            Time = DateTimeOffset.FromUnixTimeSeconds(item.Time).ToString("yyyy-MM-ddTHH:mm:sszzz"),
            Score = item.Score,
            CommentCount = item.Descendants
        };

        _cache.Set(cacheKey, story, TimeSpan.FromMinutes(2));
        return story;
    }
}