"""Browser-only synthetic fixtures; never post to a task service."""
import copy
import json
import math
from pathlib import Path
from urllib.request import urlopen
from playwright.sync_api import sync_playwright

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Evidence/GazeConsole"
BASE = "http://127.0.0.1:8765"
base = json.load(urlopen(BASE + "/console/snapshot"))
results = []
layout = dict(available=True, minX=-2.2, maxX=2.2, minY=-2, maxY=1.4)
for name, values in dict(movie=(-1.6,-.9,3.2,1.8), sphere=(-.1,-1.5,.2,.2),
                         tray=(-.3,-1.65,.6,.07), task=(-.4,-1.75,.8,.55)).items():
    layout[name] = dict(zip(["x","y","width","height"], values), available=True)
rows = [dict(id=i,time=i/10,valid=True,projected=True,
             x=.16*math.sin(i*2.3) if i<60 else .65+.5*math.sin(i*2.3),
             y=-1.4+.12*math.cos(i*1.7) if i<60 else .25+.25*math.cos(i*1.7),
             target="task" if i<60 else "movie",taskVisible=True,movieVisible=True) for i in range(100)]
gaze = dict(revision="gaze-console-front-plane-v1", source="tracked_eyes", status="tracked eyes",
            phase="Running", now=10,frame=1,layout=layout,samples=rows)
fixture = copy.deepcopy(base)
fixture.update(session="SYNTHETIC_GAZE_UI",telemetryAge=.1,signals=[],observedSignals=[],sensors={},windows=[])
fixture["task"] = dict(sessionType="synthetic",participantCode="SYNTHETIC_GAZE_UI",stage="bioadaptive",
                       level=0,config={},gazeOverview=gaze)
with sync_playwright() as pw:
    browser = pw.chromium.launch(channel="msedge",headless=True)
    page = browser.new_page(viewport={"width":1440,"height":1100},device_scale_factor=1)
    errors=[]
    page.on("pageerror", lambda e: errors.append(str(e)))
    active = [base]
    page.route("**/console/snapshot*", lambda route: route.fulfill(json=active[0]))
    def show(payload, status):
        active[0]=payload
        page.goto(BASE+"/?gaze_ui_test="+str(len(results))+"#physiology")
        page.locator("#gaze-status").filter(has_text=status).wait_for()
        assert page.locator("#gaze-status").inner_text()==status
        results.append(status)
    def count(selector, n):
        assert page.locator(selector).count()==n,(selector,page.locator(selector).count(),n)
    empty=copy.deepcopy(base);empty.update(task=None,session=None)
    show(empty,"Waiting for Unity")
    count(".gaze-history-dot",0);count("#gaze-current-point",0)
    page.locator(".ec-gaze-panel").screenshot(path=str(OUT/"waiting-for-unity.png"))
    show(fixture,"Tracked eyes")
    count(".gaze-history-dot",100);count("#gaze-current-point",1)
    assert [page.locator("#gaze-"+name).inner_text() for name in ["task","movie","valid"]]==["60%","40%","100%"]
    page.locator(".ec-gaze-panel h2").evaluate("el=>el.textContent='Gaze overview · Synthetic test preview'")
    page.locator(".ec-gaze-panel").screenshot(path=str(OUT/"synthetic-gaze-preview.png"))
    page.locator('[data-gaze-mode="live"]').click();count(".gaze-history-dot",0);count("#gaze-current-point",1)
    page.locator('[data-gaze-mode="distribution"]').click();count(".gaze-history-dot",100)
    for width in [1440,1080,736,560,360,320]:
        page.set_viewport_size({"width":width,"height":1100})
        assert page.evaluate("document.documentElement.scrollWidth<=innerWidth"),width
    page.set_viewport_size({"width":360,"height":1100})
    page.locator(".ec-gaze-panel").screenshot(path=str(OUT/"synthetic-gaze-mobile.png"))
    page.set_viewport_size({"width":1440,"height":1100})
    invalid=copy.deepcopy(fixture)
    for r in invalid["task"]["gazeOverview"]["samples"]:r.update(valid=False,projected=False,target="invalid")
    show(invalid,"Tracking unavailable");count(".gaze-history-dot",0);count("#gaze-current-point",0)
    assert page.locator("#gaze-valid").inner_text()=="0%"
    assert page.locator("#gaze-task").inner_text()=="—"
    partial=copy.deepcopy(fixture)
    for r in partial["task"]["gazeOverview"]["samples"][90:]:r.update(valid=False,projected=False,target="invalid")
    show(partial,"Tracking unavailable");count(".gaze-history-dot",90);count("#gaze-current-point",0)
    assert page.locator("#gaze-valid").inner_text()=="90%"
    stale=copy.deepcopy(fixture);stale["telemetryAge"]=4
    show(stale,"Telemetry stale");count("#gaze-current-point",0)
    outside=copy.deepcopy(fixture);outside["task"]["gazeOverview"]["samples"][-1]["x"]=99
    show(outside,"Outside projection");count(".gaze-history-dot",99);count("#gaze-current-point",0)
    paused=copy.deepcopy(fixture);paused["task"]["gazeOverview"]["phase"]="Paused"
    show(paused,"Paused");count(".gaze-history-dot",0);count("#gaze-current-point",0)
    old=copy.deepcopy(fixture);old["task"].pop("gazeOverview")
    show(old,"Gaze telemetry unavailable");count(".gaze-history-dot",0)
    head=copy.deepcopy(fixture);head["task"]["gazeOverview"]["source"]="head_pose"
    show(head,"Gaze telemetry unavailable");count(".gaze-history-dot",0)
    missing=copy.deepcopy(fixture);missing["task"]["gazeOverview"]["layout"]=None
    show(missing,"Projection unavailable");count(".gaze-history-dot",0)
    assert not errors,errors
    browser.close()
OUT.joinpath("browser-validation.json").write_text(json.dumps(dict(result="PASS",cases=results,
    widths=[1440,1080,736,560,360,320],pageErrors=errors,evidence="Synthetic browser fixtures only; no POST or task session"),indent=2))
print("PASS: gaze status, dots, metrics, mode switch, six responsive widths; no page errors")
