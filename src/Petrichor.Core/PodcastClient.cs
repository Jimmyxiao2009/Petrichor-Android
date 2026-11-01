using System;using System.Collections.Generic;using System.Linq;using System.Threading;using System.Threading.Tasks;using Newtonsoft.Json.Linq;
namespace Petrichor.Core {
 public sealed class DjRadio {
  public long Id {get;set;} public string Name {get;set;} public string CoverUrl {get;set;} public string DjName {get;set;} public string Description {get;set;} public string Category {get;set;}
  public int ProgramCount {get;set;} public long SubCount {get;set;} public bool Subscribed {get;set;}
  internal static DjRadio From(JToken r)=>new DjRadio {Id=(long?)r["id"]??0,Name=(string)r["name"]??"",CoverUrl=(string)(r["picUrl"]??r["intervenePicUrl"]??r["coverUrl"])??"",DjName=(string)(r["dj"]?["nickname"]??r["djName"])??"",Description=(string)(r["desc"]??r["rcmdtext"]??r["rcmdText"])??"",Category=(string)r["category"]??"",ProgramCount=(int?)r["programCount"]??0,SubCount=(long?)(r["subCount"]??r["subedCount"])??0,Subscribed=(bool?)(r["subed"]??r["subscribed"])??false};
 }
 public sealed class DjProgram {
  public long Id {get;set;} public string Name {get;set;} public string CoverUrl {get;set;} public string Description {get;set;} public int DurationMs {get;set;} public DateTimeOffset CreatedAt {get;set;} public long ListenerCount {get;set;} public long SerialNumber {get;set;} public string RadioName {get;set;}
  public long RadioId {get;set;}
  /// <summary>可直接交给播放器的音频；ProgramId 指回本节目。</summary>
  public Track Track {get;set;}
  internal static DjProgram From(JToken p){
   var radio=p["radio"] as JObject;var cover=(string)(p["coverUrl"]??radio?["picUrl"])??"";
   var program=new DjProgram {Id=(long?)p["id"]??0,Name=(string)p["name"]??"",CoverUrl=cover,Description=(string)p["description"]??"",DurationMs=(int?)p["duration"]??0,CreatedAt=DateTimeOffset.FromUnixTimeMilliseconds((long?)p["createTime"]??0),ListenerCount=(long?)p["listenerCount"]??0,SerialNumber=(long?)p["serialNum"]??0,RadioName=(string)radio?["name"]??""};
   program.RadioId=(long?)radio?["id"]??0;
   var song=p["mainSong"] as JObject;
   var track=song==null||song.Type==JTokenType.Null?new Track():Track.From(song);
   if(track.Id==0)track.Id=(long?)p["mainTrackId"]??0;track.ProgramId=program.Id;track.Name=program.Name.Length>0?program.Name:track.Name;
   if(track.DurationMs==0)track.DurationMs=program.DurationMs;
   // 节目 mainSong 的 artists/album 常为空或是占位，展示用电台与主播信息
   var dj=(string)((p["dj"] as JObject)?["nickname"]??(radio?["dj"] as JObject)?["nickname"]);
   if(track.Artists.Count==0||track.Artists.All(a=>string.IsNullOrWhiteSpace(a.Name)))track.Artists=new List<Artist>{new Artist{Name=string.IsNullOrWhiteSpace(dj)?program.RadioName:dj}};
   track.Album=new Album{Id=0,Name=program.RadioName,CoverUrl=cover.Length>0?cover:track.Album.CoverUrl};
   program.Track=track;return program;
  }
 }
 public sealed partial class NeteaseClient {
  public async Task<CollectionPage<DjProgram>> SearchProgramsAsync(string query,int limit=30,int offset=0,CancellationToken ct=default(CancellationToken)){
   if(string.IsNullOrWhiteSpace(query)||query.Trim().Length>100)throw new ArgumentException("请输入 1 至 100 字的搜索词",nameof(query));Page(limit,offset);
   var j=await RequestAsync("search/voice/get",Data("keyword",query.Trim(),"scene","normal","limit",limit,"offset",offset),ct).ConfigureAwait(false);
   var data=j["data"];var resources=(data?["resources"]??new JArray()).ToList();
   var items=resources.OfType<JObject>().Select(r=>r["baseInfo"] as JObject??r["program"] as JObject??r).Select(DjProgram.From).Where(p=>p.Id>0).ToList();
   return new CollectionPage<DjProgram>{Items=items,Offset=offset,HasMore=(bool?)(data?["hasMore"]??data?["more"])??resources.Count==limit};
  }
  public async Task<DjProgram> GetProgramAsync(long id,CancellationToken ct=default(CancellationToken)){
   var j=await RequestAsync("dj/program/detail",Data("id",ValidId(id)),ct).ConfigureAwait(false);
   var p=j["program"];if(p==null||p.Type==JTokenType.Null)throw new MusicApiException(404,"节目不存在");return DjProgram.From(p);
  }
  public async Task<CollectionPage<DjRadio>> GetHotRadiosAsync(int limit=30,int offset=0,CancellationToken ct=default(CancellationToken)){
   Page(limit,offset);var j=await RequestAsync("djradio/hot/v1",Data("limit",limit,"offset",offset),ct).ConfigureAwait(false);
   return new CollectionPage<DjRadio>{Items=(j["djRadios"]??new JArray()).Select(DjRadio.From).ToList(),HasMore=(bool?)j["hasMore"]??false,Offset=offset};
  }
  public async Task<CollectionPage<DjRadio>> GetSubscribedRadiosAsync(int limit=30,int offset=0,CancellationToken ct=default(CancellationToken)){
   RequireLogin();Page(limit,offset);var j=await RequestAsync("djradio/get/subed",Data("limit",limit,"offset",offset,"total",true),ct).ConfigureAwait(false);
   return new CollectionPage<DjRadio>{Items=(j["djRadios"]??new JArray()).Select(DjRadio.From).ToList(),HasMore=(bool?)j["hasMore"]??false,Offset=offset};
  }
  public async Task<SearchPage<DjRadio>> SearchRadiosAsync(string query,int limit=30,int offset=0,CancellationToken ct=default(CancellationToken)){
   var j=await SearchAsync(query,1009,limit,offset,ct).ConfigureAwait(false);var r=j["result"];
   return new SearchPage<DjRadio>{Items=(r?["djRadios"]??new JArray()).Select(DjRadio.From).ToList(),Total=(int?)r?["djRadiosCount"]??0,Limit=limit,Offset=offset};
  }
  public async Task<DjRadio> GetRadioAsync(long id,CancellationToken ct=default(CancellationToken)){
   var j=await RequestAsync("djradio/v2/get",Data("id",ValidId(id)),ct).ConfigureAwait(false);var r=j["data"]??j["djRadio"];
   if(r==null||r.Type==JTokenType.Null)throw new MusicApiException(404,"播客不存在");return DjRadio.From(r);
  }
  // ascending: true 从第一期开始，false 从最新一期开始。
  public async Task<CollectionPage<DjProgram>> GetRadioProgramsAsync(long radioId,int limit=30,int offset=0,bool ascending=false,CancellationToken ct=default(CancellationToken)){
   Page(limit,offset);var j=await RequestAsync("dj/program/byradio",Data("radioId",ValidId(radioId),"limit",limit,"offset",offset,"asc",ascending),ct).ConfigureAwait(false);
   return new CollectionPage<DjProgram>{Items=(j["programs"]??new JArray()).Select(DjProgram.From).Where(x=>x.Track.Id>0).ToList(),HasMore=(bool?)j["more"]??false,Offset=offset};
  }
  public async Task SetRadioSubscribedAsync(long id,bool subscribed,CancellationToken ct=default(CancellationToken)){RequireLogin();await RequestAsync("djradio/"+(subscribed?"sub":"unsub"),Data("id",ValidId(id)),ct).ConfigureAwait(false);}
 }
}
