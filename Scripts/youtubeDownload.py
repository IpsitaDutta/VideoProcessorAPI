import sys
from youtube_transcript_api import YouTubeTranscriptApi

def download_transcript(video_id):
    try:
        transcript = YouTubeTranscriptApi.get_transcript(video_id)
        text_only = ' '.join([entry['text'] for entry in transcript])
        return text_only
    except Exception as e:
        return str(e)

if __name__ == "__main__":
    if len(sys.argv) != 2:
        print("Usage: python download_transcript.py <video_id>")
        sys.exit(1)

    video_id = sys.argv[1]
    transcript = download_transcript(video_id)
    print(transcript)
