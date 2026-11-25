namespace Firefly.Api.Services.Storage
{
    public class FileStorageSettings
    {
        public const string SectionName = "Supabase";

        public string Url { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string Bucket { get; set; } = "uploads"; 
    }
}
