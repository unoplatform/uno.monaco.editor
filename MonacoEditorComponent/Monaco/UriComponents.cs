namespace Monaco
{
    public interface UriComponents
    {
        string? Authority { get; set; }
        string? Fragment { get; set; }
        string? Path { get; set; }
        string? Query { get; set; }
        string? Scheme { get; set; }
    }
}
