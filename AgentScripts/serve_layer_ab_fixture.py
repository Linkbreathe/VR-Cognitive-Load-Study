"""Disposable synthetic questionnaire preview; never attaches to the active study service."""
from pathlib import Path
import sys
from datetime import datetime, timezone

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'Tools/AdaptiveTraining'))
from service import StudyService, StudyHTTPServer, make_handler
from test_layer_ab import ab_packet

output=ROOT/'Evidence/LayerAB/UIFixture'/datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S')
service=StudyService(output,test_state_dir=output/'state')
packet=ab_packet('questionnaire_2');packet['sessionId']='SYNTHETIC_UI_ONLY';packet['participantCode']='SYNTHETIC_UI_ONLY'
service.receive(packet)
server=StudyHTTPServer(('127.0.0.1',8879),make_handler(service))
print('Synthetic questionnaire preview: http://127.0.0.1:8879/ab-study',flush=True)
try:server.serve_forever()
finally:server.server_close()
