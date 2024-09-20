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

			var video = GetYouTubeVideo(url);

			if (video == null)
			{
				return("Invalid YouTube URL or video not found.");
			}
			try
			{
				var filePath = Path.Combine(Directory.GetCurrentDirectory(), Regex.Replace(video.Title,"[^a-zA-Z]","") + ".mp4");

				using (var webClient = new WebClient())
				{
					webClient.DownloadFile(video.Uri, filePath);
				}

				var audioPath = Path.Combine(Directory.GetCurrentDirectory(), Regex.Replace(video.Title, "[^a-zA-Z]", "") + "_audio.wav");

				// convert video to audio
				ConvertAudioFromVideo(filePath, audioPath, Regex.Replace(video.Title, "[^a-zA-Z]", ""));

				// use model to get the transcript from the audio file.

				var modelFileName = "Test";//@"C:\Users\ipdutta\Downloads\ggml-base.bin";  // update your local path
				var audioFileName = Path.Combine(Directory.GetCurrentDirectory(), Regex.Replace(video.Title, "[^a-zA-Z]", "") + "16kHz_audio.wav");

				if (!System.IO.File.Exists(modelFileName))
				{
					using var modelStream = await WhisperGgmlDownloader.GetGgmlModelAsync(GgmlType.Base);
					using var fileWriter = System.IO.File.OpenWrite(modelFileName);
					await modelStream.CopyToAsync(fileWriter);
				}

				using var whisperFactory = WhisperFactory.FromPath(modelFileName);
				using var processor = whisperFactory.CreateBuilder()
					.WithLanguage("auto")
					.Build();

				using var fileStream = System.IO.File.OpenRead(audioFileName);
				Console.WriteLine(fileStream.ToString());

				StringBuilder transcriptBuilder = new StringBuilder();
				await foreach (var result in processor.ProcessAsync(fileStream))
				{
					transcriptBuilder.AppendLine($"{result.Start}->{result.End}: {result.Text}");
					//Console.WriteLine($"{result.Start}->{result.End}:{result.Text}");
				}

				string videoTranscript = transcriptBuilder.ToString();

				// Prepare the prompt for summarization
				string prompt = $"Please summarize the following transcript and list key notes and 3 smart tags:\n\n{videoTranscript}\n\n" +
					 "MAKE SURE TO RETURN the summarize result in JSON format like this:\n" +
					 "{\n" +
					 "  \"KeyNotes\": [\"note1\", \"note2\"],\n" +
					 "  \"Summary\": \"your summary here\",\n" +
					 "  \"SmartTags\":[\"tag1\", \"tag2\"]\n" +
					 "}";

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

		static YouTubeVideo GetYouTubeVideo(string url)
		{
			try
			{
				var youTube = YouTube.Default;
				return youTube.GetVideo(url);
			}
			catch
			{
				return null;
			}
		}

		public static void ConvertAudioFromVideo(string inputPath, string outputPath, string title)
		{
			var inputFile = new MediaFile { Filename = inputPath };
			var outputFile = new MediaFile { Filename = outputPath };
			var conversionOptions = new ConversionOptions
			{
				AudioSampleRate = AudioSampleRate.Hz22050
			};
			using (var engine = new Engine("C:\\Users\\ipdutta\\Downloads\\bin 1\\bin\\ffmpeg.exe"))
			{
				engine.Convert(inputFile, outputFile, conversionOptions);
			}

			// since the model needs 16kHz sample rate, using NAusdio.wave to sample it at 16kHz
			// this generate another file, TODO : delete the previously generated file which is sampled at 22050Hz.
			using (var reader = new WaveFileReader(outputPath))
			{
				var outputFormat = new WaveFormat(16000, reader.WaveFormat.Channels);

				using (var resampler = new MediaFoundationResampler(reader, outputFormat))
				{
					using (var writer = new WaveFileWriter(Path.Combine(Directory.GetCurrentDirectory(), title + "16kHz_audio.wav"), outputFormat))
					{
						byte[] buffer = new byte[1024];
						int bytesRead;
						while ((bytesRead = resampler.Read(buffer, 0, buffer.Length)) > 0)
						{
							writer.Write(buffer, 0, bytesRead);
						}
					}
				}
			}
			Console.WriteLine($"Audio extracted to: {outputPath}");
		}

		private static async Task DownloadModel(string fileName, GgmlType ggmlType)
		{
			Console.WriteLine($"Downloading Model {fileName}");
			using var modelStream = await WhisperGgmlDownloader.GetGgmlModelAsync(ggmlType);
			using var fileWriter = System.IO.File.OpenWrite(fileName);
			await modelStream.CopyToAsync(fileWriter);
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
	}
}
