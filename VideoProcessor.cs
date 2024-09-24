using Newtonsoft.Json;

namespace VideoProcessorAPI
{
	public class VideoProcessor
	{
		[JsonProperty("KeyNotes")]
		public Dictionary<string, string> KeyNotes { get; set; }

		[JsonProperty("Summary")]
		public string Summary { get; set; }

		[JsonProperty("SmartTags")]
		public List<string> SmartTags { get; set; }

		[JsonProperty("Title")]
		public string Title { get; set; }

		[JsonProperty("Thumbnail")]
		public string Thumbnail { get; set; }

		[JsonProperty("Url")]
		public string Url { get; set; }

		[JsonProperty("Images")]
		public List<string>? Images { get; set; }
	}
}
