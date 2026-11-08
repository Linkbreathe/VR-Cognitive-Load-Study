import copy
import unittest
import json
import tempfile
from pathlib import Path
from unittest.mock import patch
from lsl_sources import discover, validate_config, stream_identity, matching_streams, observation_values
from service import physiology_display_status


class SourceSelectionTests(unittest.TestCase):
    def setUp(self):
        self.eeg = json.loads(Path(__file__).with_name("lsl-config.example.json").read_text())["streams"][0]

    def test_discovery_lists_all_resolved_outlets(self):
        import pylsl
        outlets = [pylsl.StreamInfo("Event outlet", "Markers", 1, 0, "string", ""),
                   pylsl.StreamInfo("Other amplifier", "EEG", 2, 128, "float32", "custom")]
        with patch.object(pylsl, "resolve_streams", return_value=outlets), patch.object(pylsl, "StreamInlet") as inlet:
            inlet.return_value.info.side_effect = outlets
            rows = discover()["streams"]
        self.assertEqual([row["name"] for row in rows], [info.name() for info in outlets])

    def test_eeg_profile_allows_other_outlet_identities(self):
        self.assertEqual(validate_config({"streams":[self.eeg]})["streams"][0]["channels"], list(range(8)))
        validate_config({"streams":[dict(self.eeg,name="Renamed amplifier",source_id="new-id")]})
        for origin in ("unknown", "synthetic", "recorded_csv_replay", "recorded_xdf_replay"):
            validate_config({"streams":[dict(self.eeg, data_origin=origin)]})
        for change in (
                       dict(kind="ECG"), dict(channel_labels=list(reversed(self.eeg["channel_labels"])))):
            with self.subTest(change=change), self.assertRaises(ValueError):
                validate_config({"streams":[dict(self.eeg, **change)]})

    def test_invalid_indices_rates_origins_and_differential_are_rejected(self):
        for change in [dict(channels=[4]),dict(channels=[-1]),dict(channels=[0,0]),dict(channels=[]),
                       dict(channels=[True]),dict(sample_rate=0),dict(sample_rate=float("nan")),
                       dict(scale_to_units=float("inf")),dict(amplitude_limit=-1),dict(differential=True),
                       dict(source_id=" "),dict(data_origin="real_replay"),dict(window_seconds=60)]:
            with self.subTest(change=change),self.assertRaises(ValueError):
                validate_config({"streams":[dict(self.eeg,**change)]})

    def test_multiple_ecg_channels_need_explicit_derivation(self):
        with self.assertRaises(ValueError):validate_config({"streams":[dict(self.eeg,kind="ECG",window_seconds=30)]})

    def test_low_rate_precomputed_values_use_explicit_features_role(self):
        stream=dict(self.eeg,sample_rate=2)
        with self.assertRaises(ValueError):validate_config({"streams":[stream]})
        stream.update(kind="FEATURES",window_seconds=1)
        with self.assertRaises(ValueError):validate_config({"streams":[stream]})

    def test_empty_and_duplicate_inputs_rejected(self):
        for rows in [[],[self.eeg,copy.deepcopy(self.eeg)]]:
            with self.assertRaises(ValueError):validate_config({"streams":rows})

    def test_source_name_is_independent_of_replay_origin_and_receipt(self):
        sensor=dict(self.eeg,name="Combined amplifier",source_id="source-a",data_origin="recorded_xdf_replay",connected=True,last_sample=9)
        status=physiology_display_status({"EEG:source-a":sensor},10)
        self.assertEqual(status["sourceName"],"Combined amplifier")
        self.assertEqual(status["source"],"recorded_replay")
        self.assertEqual(status["streams"][0]["sourceId"],"source-a")
        lost=physiology_display_status({"EEG:source-a":sensor},14)
        self.assertEqual(lost["sourceName"],status["sourceName"])
        self.assertEqual(lost["connection"],"disconnected")

    def test_one_combined_name_keeps_both_channel_roles(self):
        sensor=dict(self.eeg,name="Combined amplifier",source_id="source-a",data_origin="recorded_xdf_replay",connected=True,last_sample=9)
        status=physiology_display_status({"EEG":sensor,"ECG":dict(sensor,kind="ECG")},10)
        self.assertEqual(status["sourceName"],"Combined amplifier")
        self.assertEqual([s["kind"] for s in status["streams"]],["EEG","ECG"])

    def test_missing_name_falls_back_to_id_and_unknown_never_becomes_live(self):
        status=physiology_display_status({"EEG":dict(source_id="device-42",data_origin="unknown")},10)
        self.assertEqual(status["sourceName"],"device-42")
        self.assertEqual(status["source"],"unknown")

    def test_xml_replay_marker_and_channel_labels_are_retained(self):
        import pylsl
        info=pylsl.StreamInfo("Test replay","EEG",2,250,"float32","test-source")
        info.desc().append_child("xdf_replay").append_child_value("recorded_data","true")
        info.desc().append_child("channels").append_child("channel").append_child_value("label","C1")
        identity=stream_identity(info)
        self.assertTrue(identity["recorded"])
        self.assertEqual(identity["channel_labels"],["C1"])
        self.assertEqual(identity["source_id"],"test-source")

    def test_shared_ids_are_disambiguated_by_uid_and_descriptor(self):
        class Info:
            def __init__(self,uid,name,kind):self.u,self.n,self.t=uid,name,kind
            def uid(self):return self.u
            def name(self):return self.n
            def type(self):return self.t
            def source_id(self):return "shared"
        a,b,c=Info("uid-a","Signals","eeg"),Info("uid-b","Markers","event"),Info("uid-c","Signals","eeg")
        selection=dict(uid="uid-a",source_id="shared",name="Signals",stream_type="eeg")
        self.assertEqual(matching_streams([b,c,a],selection),[a])
        self.assertEqual(matching_streams([b,c],selection),[])
        self.assertEqual(matching_streams([a],dict(selection,name="Wrong")),[])
        self.assertEqual(matching_streams([a,b],dict(source_id="shared")),[a,b])

    def test_receive_only_accepts_irregular_text_and_missing_source_id_with_uid(self):
        stream=dict(self.eeg,kind="OBSERVE",sample_rate=0,uid="chosen-marker",source_id="",channel_format=3,window_seconds=1)
        validate_config({"streams":[stream]})
        sample=[["stimulus/start","condition=hard"]]
        stream["channel_labels"]=["event","condition"]
        self.assertEqual([v["value"] for v in observation_values(stream,sample)],sample[0])
        for change in [dict(scale_to_units=2),dict(differential=True),dict(sample_rate=-1)]:
            with self.assertRaises(ValueError):validate_config({"streams":[dict(stream,**change)]})

    def test_irregular_connected_idle_is_not_a_dropout(self):
        sensor=dict(source_id="markers",uid="marker-instance",stream_type="Markers",name="Markers",connected=True,sample_rate=0,last_sample=1)
        status=physiology_display_status({"OBSERVE":sensor},10)
        self.assertEqual(status["connection"],"idle")
        self.assertEqual(status["streams"][0]["uid"],"marker-instance")
        self.assertEqual(status["streams"][0]["streamType"],"Markers")
        sensor["connected"]=False
        self.assertEqual(physiology_display_status({"OBSERVE":sensor},10)["connection"],"disconnected")

    def test_observation_retains_strings_and_marks_nonfinite_values(self):
        stream=dict(channels=[0,1,2],channel_labels=["label","count","invalid"])
        values=observation_values(stream,[["<event>",42,float("nan")]])
        self.assertEqual([v["value"] for v in values],["<event>",42,None])

    def test_receive_only_records_text_without_creating_features(self):
        from service import StudyService
        from test_stage2 import packet
        with tempfile.TemporaryDirectory() as folder:
            service=StudyService(folder);p=packet();service.receive(p)
            stream=dict(kind="OBSERVE",uid="marker-uid",source_id="shared",synthetic=True,data_origin="recorded_xdf_replay")
            service.record_signal(stream,[1],[2],[["trial_start"]],0,"shared")
            record=json.loads((Path(folder)/"synthetic"/p["sessionId"]/"raw_signals.jsonl").read_text())
            self.assertEqual(record["samples"],[["trial_start"]])
            self.assertEqual(record["stream"]["uid"],"marker-uid")
            self.assertEqual(service.status()["features"],[])
            p.update(sessionId="participant_guard",sessionType="participant");service.receive(p)
            with self.assertRaises(ValueError):service.record_signal(stream,[1],[2],[["trial_start"]],0,"shared")


if __name__=="__main__":unittest.main()
