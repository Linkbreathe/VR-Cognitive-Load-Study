using System;
using System.IO;
using System.Linq;
using EvidenceVR;
using UnityEngine;

public static class FilmPoolValidation
{
    static int checks;
    static void Check(bool condition, string message) { checks++; if(!condition)throw new Exception(message); }
    static ContinuousMediaBank Bank()
    {
        var bank=new ContinuousMediaBank { revision=AdaptiveMedia.ContinuousRevision, catalogVersion="synthetic-film-fixture",
            selectionRevision=ContinuousPlaylistAllocation.Revision };
        bank.playlists=Enumerable.Range(0,6).Select(i=>new ContinuousPlaylist{id="fixture-"+i,sha256="hash-"+i,duration=300,
            segments=new[]{new ContinuousSegment{mediaId="source-"+i,sourceVideoHash="sourcehash-"+i,start=0,duration=300}}}).ToArray();
        bank.randomizedPlaylistIds=bank.playlists.Select(p=>p.id).ToArray();return bank;
    }
    public static object Run()
    {
        checks=0;var bank=Bank();
        for(int seed=1;seed<=250;seed++)
        {
            var history=new ContinuousMediaHistory();
            for(int session=0;session<18;session++)
            {
                string stage=new[]{"fixed_exposure","layer_a","layer_b"}[session%3];
                var choice=ContinuousPlaylistAllocation.Choose(bank,history,"session-"+session,stage,seed);
                Check(choice.playlistId==ContinuousPlaylistAllocation.Choose(bank,history,"session-"+session,stage,seed).playlistId,"Deterministic choice");
                Check(!history.reservations.Skip(Math.Max(0,history.reservations.Length-2)).Any(r=>r.playlistId==choice.playlistId),"No repeats within a three-stage run, including cycle boundaries");
                Check(choice.previousReservations==session/6,"Exhaust unseen pool before reuse");
                history.reservations=history.reservations.Concat(new[]{choice}).ToArray();
                Check(object.ReferenceEquals(choice,ContinuousPlaylistAllocation.Choose(bank,history,"session-"+session,stage,seed)),"Same session is idempotent");
                if(session%6==5)Check(history.reservations.Skip(session-5).Select(r=>r.playlistId).Distinct().Count()==6,"Six distinct choices per cycle");
            }
        }
        string dir=Path.GetFullPath("Evidence/FilmLibrary-20261112/allocator");Directory.CreateDirectory(dir);
        string scope="isolated-"+Guid.NewGuid().ToString("N");
        var first=ContinuousPlaylistAllocation.Reserve(dir,scope,bank,"one","fixed_exposure",44,dir);
        var retry=ContinuousPlaylistAllocation.Reserve(dir,scope,bank,"one","fixed_exposure",44,dir);
        var next=ContinuousPlaylistAllocation.Reserve(dir,scope,bank,"two","layer_a",44,dir);
        Check(first.playlistId==retry.playlistId && first.playlistId!=next.playlistId,"Disk history survives independent calls and retries");
        Check(File.Exists(Path.Combine(dir,"continuous-media-selection.json")),"Session selection artifact");
        var corrupted=Bank();corrupted.playlists[1].sha256=corrupted.playlists[0].sha256;
        bool rejected=false;try{ContinuousPlaylistAllocation.Choose(corrupted,new ContinuousMediaHistory(),"x","layer_b",1);}catch(InvalidDataException){rejected=true;}
        Check(rejected,"Duplicate content rejected");
        rejected=false;try{ContinuousPlaylistAllocation.Choose(bank,new ContinuousMediaHistory{reservations=null},"x","layer_b",1);}catch(InvalidDataException){rejected=true;}
        Check(rejected,"Malformed history cannot silently reset");
        string text="PASS: "+checks+" film allocation checks; isolated synthetic histories only.";
        File.WriteAllText(Path.Combine(dir,"validation.txt"),text);Debug.Log(text);return new{checks};
    }
}
