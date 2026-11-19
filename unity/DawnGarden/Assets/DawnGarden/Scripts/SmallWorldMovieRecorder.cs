using System;
using System.Collections;
#if !UNITY_WEBGL || UNITY_EDITOR
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
#endif
using UnityEngine;

namespace DawnGarden
{
    public sealed class SmallWorldMovieRecorder : MonoBehaviour
    {
        public const int Width=1920,Height=1080,Fps=24;
#if UNITY_WEBGL && !UNITY_EDITOR
        public int Frames=>0;
        public double Elapsed=>0;
        public bool Recording=>false;
        public void Begin(string output,string ffmpeg)=>throw new PlatformNotSupportedException("Movie recording requires the native player.");
        public void AttachAudio(GameObject listener){}
        public void AudioBlock(float[] data,int channels,int source){}
        public IEnumerator Finish(){yield break;}
#else
        sealed class Frame {public byte[] pixels;public int copies;}
        readonly BlockingCollection<Frame> queue=new BlockingCollection<Frame>(6);
        readonly object audioLock=new object();
        readonly StringBuilder encoderLog=new StringBuilder();
        Process encoder;Thread writer;Stopwatch clock;RenderTexture screen,scaled;Texture2D pixels;
        BinaryWriter audio;string directory;int scheduled,audioChannels=2,sampleRate;long audioFrames;
        double audioDspStart;volatile bool active;bool finishing;Exception writerError;int tapId;
        public int Frames=>scheduled;
        public double Elapsed=>clock?.Elapsed.TotalSeconds??0;
        public bool Recording=>active;
        public void Begin(string output,string ffmpeg)
        {
            directory=output;Directory.CreateDirectory(directory);sampleRate=AudioSettings.outputSampleRate;audioDspStart=AudioSettings.dspTime;
            audio=new BinaryWriter(File.Open(Path.Combine(directory,"game-audio.wav"),FileMode.Create));audio.Write(new byte[44]);
            string orientation=SystemInfo.graphicsUVStartsAtTop?"":" -vf vflip";
            var info=new ProcessStartInfo {FileName=ffmpeg,Arguments="-hide_banner -loglevel warning -y -f rawvideo -pix_fmt rgb24 -s 1920x1080 -r 24 -i pipe:0"+orientation+" -an -c:v libx264 -preset veryfast -crf 20 -pix_fmt yuv420p -g 48 -keyint_min 48 -sc_threshold 0 -movflags +frag_keyframe+empty_moov+default_base_moof "+Quote(Path.Combine(directory,"game-silent.mp4")),UseShellExecute=false,RedirectStandardInput=true,RedirectStandardError=true,CreateNoWindow=true};
            encoder=new Process {StartInfo=info};encoder.ErrorDataReceived+=(s,e)=>{if(e.Data!=null)lock(encoderLog)if(encoderLog.Length<30000)encoderLog.AppendLine(e.Data);};
            if(!encoder.Start())throw new InvalidOperationException("Video encoder did not start");encoder.BeginErrorReadLine();
            screen=new RenderTexture(Screen.width,Screen.height,0);scaled=new RenderTexture(Width,Height,0);pixels=new Texture2D(Width,Height,TextureFormat.RGB24,false);
            clock=Stopwatch.StartNew();active=true;writer=new Thread(WriteFrames){IsBackground=true,Name="Small World movie encoder"};writer.Start();StartCoroutine(Capture());
        }
        static string Quote(string text)=>"\""+text.Replace("\\","\\\\").Replace("\"","\\\"")+"\"";
        void WriteFrames()
        {
            try{foreach(var frame in queue.GetConsumingEnumerable())for(int k=0;k<frame.copies;k++)encoder.StandardInput.BaseStream.Write(frame.pixels,0,frame.pixels.Length);encoder.StandardInput.Close();}
            catch(Exception e){writerError=e;}
        }
        IEnumerator Capture()
        {
            while(active)
            {
                yield return new WaitForEndOfFrame();if(!active)break;if(writerError!=null)throw new InvalidOperationException("Video encoder failed",writerError);
                int wanted=(int)(Elapsed*Fps)+1;if(wanted<=scheduled||queue.Count>=5)continue;
                ScreenCapture.CaptureScreenshotIntoRenderTexture(screen);Graphics.Blit(screen,scaled);var previous=RenderTexture.active;
                RenderTexture.active=scaled;pixels.ReadPixels(new Rect(0,0,Width,Height),0,0,false);RenderTexture.active=previous;
                var frame=new Frame {pixels=pixels.GetRawTextureData<byte>().ToArray(),copies=wanted-scheduled};
                if(queue.TryAdd(frame))scheduled=wanted;
            }
        }
        public void AttachAudio(GameObject listener)
        {
            var tap=listener.GetComponent<SmallWorldMovieAudio>()??listener.AddComponent<SmallWorldMovieAudio>();tap.owner=this;tapId=tap.GetInstanceID();
        }
        public void AudioBlock(float[] data,int channels,int source)
        {
            if(!active||source!=tapId)return;
            lock(audioLock)
            {
                if(!active||audio==null)return;
                if(audioFrames==0)audioChannels=channels;if(channels!=audioChannels)return;
                long expected=(long)((AudioSettings.dspTime-audioDspStart)*sampleRate);
                if(expected-audioFrames>data.Length/channels*3)
                    while(audioFrames<expected){for(int c=0;c<audioChannels;c++)audio.Write((short)0);audioFrames++;}
                foreach(float value in data)audio.Write((short)(Math.Max(-1,Math.Min(1,value))*32767));audioFrames+=data.Length/channels;
            }
        }
        public IEnumerator Finish()
        {
            if(finishing)yield break;finishing=true;active=false;clock?.Stop();queue.CompleteAdding();
            float deadline=Time.realtimeSinceStartup+60;while(writer!=null&&writer.IsAlive&&Time.realtimeSinceStartup<deadline)yield return null;
            if(writer!=null&&writer.IsAlive)throw new TimeoutException("Video encoder pipe did not close");
            while(encoder!=null&&!encoder.HasExited&&Time.realtimeSinceStartup<deadline)yield return null;
            lock(audioLock)
            {
                if(audio!=null){long bytes=audioFrames*audioChannels*2;audio.Seek(0,SeekOrigin.Begin);audio.Write(Encoding.ASCII.GetBytes("RIFF"));audio.Write((int)(bytes+36));audio.Write(Encoding.ASCII.GetBytes("WAVEfmt "));audio.Write(16);audio.Write((short)1);audio.Write((short)audioChannels);audio.Write(sampleRate);audio.Write(sampleRate*audioChannels*2);audio.Write((short)(audioChannels*2));audio.Write((short)16);audio.Write(Encoding.ASCII.GetBytes("data"));audio.Write((int)bytes);audio.Dispose();audio=null;}
            }
            lock(encoderLog)File.WriteAllText(Path.Combine(directory,"encoder.log"),encoderLog.ToString());
            if(writerError!=null||encoder==null||!encoder.HasExited||encoder.ExitCode!=0)throw new InvalidOperationException("Movie encoding failed; inspect encoder.log");
            screen.Release();scaled.Release();Destroy(screen);Destroy(scaled);Destroy(pixels);
        }
        void OnApplicationQuit(){active=false;try{queue.CompleteAdding();}catch(InvalidOperationException){}lock(audioLock){audio?.Dispose();audio=null;}}
#endif
    }
    public sealed class SmallWorldMovieAudio : MonoBehaviour
    {
        public SmallWorldMovieRecorder owner;
        int identity;
        void Awake(){identity=GetInstanceID();}
        void OnAudioFilterRead(float[] data,int channels){if(!ReferenceEquals(owner,null))owner.AudioBlock(data,channels,identity);}
    }
}
