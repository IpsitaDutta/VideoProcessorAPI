using System;
using System.IO;
using System.Net;
using VideoLibrary;
using Whisper.net;
using System.Threading.Tasks;
using Whisper.net.Ggml;
using System.Diagnostics;
using NAudio.Wave;
using MediaToolkit;
using MediaToolkit.Model;
using MediaToolkit.Options;
using Microsoft.VisualBasic;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using Azure.AI.OpenAI;
using Azure;
using OpenAI.Chat;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Newtonsoft.Json.Linq;
using static MediaToolkit.Model.Metadata;

namespace VideoProcessorAPI.Controllers
{
	[ApiController]
	[Route("/")]
	public class VideoProcessorController : ControllerBase
	{

		private readonly ILogger<VideoProcessorController> _logger;

		public VideoProcessorController(ILogger<VideoProcessorController> logger)
		{
			_logger = logger;
		}

		[HttpGet("GetVideoProcessor")]
		public async Task<string> ProcessVideo(string url)
		{
			if (url.Length == 0)
			{
				return ("Url is Null");
			}
			try
			{
				string videoId = ExtractVideoId(url);

				string videoTranscript = GetTranscript(videoId);

				string videoThumbnail = $"https://img.youtube.com/vi/{videoId}/default.jpg";

				string videoTitle = await GetVideoTitle(videoId);


				if ((url == null) && (videoTitle == null) && (videoTranscript == null))
				{
					return "Could not extract details from URL provided.";
				}

				// Prepare the prompt for summarization
				string prompt = $"Please summarize the following video from title, link and transcript and list key notes with timestamp and 3 smart tags:\n\n{videoTitle} {url} {videoTranscript}\n\n" +
					 "MAKE SURE TO RETURN the summarize result in JSON format like this:\n" +
					 "{\n" +
					 "  \"KeyNotes\": [\"timestamp1\":\"note1\", \"timestamp2\":\"note2\"],\n" +
					 "  \"Summary\": \"your summary here\",\n" +
					 "  \"SmartTags\":[\"tag1\", \"tag2\"],\n" +
					 $"  \"Title\":\"{videoTitle}\",\n" +
					 $"  \"Thumbnail\":\"{videoThumbnail}\"\n" +
					 $"  \"Url\":\"{url}\"\n" +
					 "}\n" +
					 "DO NOT make any changes to the provided Title, Thumbnail and Url";

				return await GetResultsFromGPT(prompt);
			}
			catch (Exception ex)
			{
				return($"Error: {ex.Message}");
			}
		}

		[HttpGet("GetBlogProcessor")]
		public async Task<string> ProcessBlog(string htmlContent)
		{
			if (htmlContent.Length == 0)
			{
				return "Content is null";
			}

			string prompt = $"I have an HTML snippet that needs improvement. Please enhance it using only inline styles (do not use any external CSS). Here are the specific issues that need to be fixed:\r\nEnsure that content such as HTML is properly detected even when there is a banner or div between different sections.\r\nFix any broken or malformed <img> elements, particularly those that may be lazy-loaded, ensuring the correct image is displayed.\r\nEnsure that headings (<h1>, <h2>, etc.) resemble the styles of the original blog, such as appropriate font sizes, colors, and alignment.\r\nCorrect indentation across the entire document for better readability and consistency.\r\nPrevent images from being oversized; ensure they fit the available space appropriately.\r\nEnsure that images maintain a consistent border radius effect, if one was present in the original design.\r\nPlease generate a new version of the HTML with these improvements applied inline. Here is the HTML snippet {htmlContent}";

			return await GetResultsFromGPT(prompt);

		}

		private async Task<string> GetResultsFromGPT(string prompt)
		{
			string openAIEndpoint = "https://ujguptaazureopenai.openai.azure.com/";
			string openAIKey = "2038a02e8a4648dc9366f2dc2c327dbc";

			AzureOpenAIClient client = new AzureOpenAIClient(new Uri(openAIEndpoint), new AzureKeyCredential(openAIKey));

			var chatClient = client.GetChatClient("gpt-35-turbo");

			// Prepare the prompt for summarization
			
			// Create a chat message
			var chatMessage = ChatMessage.CreateUserMessage(prompt);

			// Send the message and get the response
			var response = await chatClient.CompleteChatAsync(new[] { chatMessage });

			return (response.Value.ToString());
		}

		private string GetTranscript(string videoId)
		{
			string pythonScript = Directory.GetCurrentDirectory() + "\\Scripts\\youtubeDownload.py";

			ProcessStartInfo start = new ProcessStartInfo
			{
				FileName = "python", // Ensure Python is in your PATH
				Arguments = $"{pythonScript} {videoId}",
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true
			};

			using (Process process = new Process())
			{
				process.StartInfo = start;
				process.Start();

				string result = process.StandardOutput.ReadToEnd();
				string error = process.StandardError.ReadToEnd();

				process.WaitForExit();

				if (!string.IsNullOrEmpty(error))
				{
					return null;
				}
				else
				{
					return("Transcript: " + result);
				}
			}
		}

		private string ExtractVideoId(string url)
		{
			var regex = new Regex(@"(?:youtube\.com\/(?:[^\/]+\/.+\/|(?:v|e(?:mbed)?\/|.*[?&]v=)|youtu\.be\/)([^""&?\/\s]{11}))");
			var match = regex.Match(url);
			return match.Success ? match.Groups[1].Value : string.Empty;
		}

		private async Task<string> GetVideoTitle(string videoId)
		{
			string apiKey = "AIzaSyC3ddgdWg8FgVZ42Gsgoj6ItU35L31xuvQ";

			string url = $"https://www.googleapis.com/youtube/v3/videos?id={videoId}&key={apiKey}&part=snippet";

			using (HttpClient client = new HttpClient())
			{
				var response = await client.GetAsync(url);

				if (response.IsSuccessStatusCode)
				{
					var json = await response.Content.ReadAsStringAsync();
					var data = JObject.Parse(json);

					// Check if items array is not empty
					if (data["items"].HasValues)
					{
						// Extract the title from the snippet
						return data["items"][0]["snippet"]["title"].ToString();
					}

					return null;
				}
				else
				{
					Console.WriteLine($"Error: {response.StatusCode}");
					return null;
				}
			}
		}

	}
}
