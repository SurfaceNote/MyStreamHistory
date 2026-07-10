namespace MyStreamHistory.Shared.Base.Contracts.Viewers;

public class ViewerFavoriteCategoryDto
{
    public string TwitchCategoryId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string BoxArtUrl { get; set; } = string.Empty;
    public int MinutesWatched { get; set; }
    public int StreamsCount { get; set; }
}
