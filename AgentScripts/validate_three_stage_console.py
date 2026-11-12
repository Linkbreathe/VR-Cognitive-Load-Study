"""Exercise the isolated port-8773 Console. No production assignments are created."""
from pathlib import Path
import json
import sys
from urllib.request import urlopen, Request
from playwright.sync_api import sync_playwright

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/"Evidence/Comparison34-20261112/browser"
OUT.mkdir(parents=True,exist_ok=True)
BASE="http://127.0.0.1:8773"
def api(path,body=None):
    request=Request(BASE+path,data=json.dumps(body).encode() if body is not None else None,headers={"Content-Type":"application/json","X-Test-Console":"engineering-model-reuse-v1"})
    return json.load(urlopen(request))
checks=[]
with sync_playwright() as pw:
    browser=pw.chromium.launch(channel="msedge",headless=True)
    page=browser.new_page(viewport={"width":1440,"height":1100})
    errors=[];page.on("pageerror",lambda error:errors.append(str(error)))
    for stage in ("fixed_exposure","layer_a","layer_b"):
        page.goto(BASE+"/tests?stage="+stage)
        page.wait_for_function("document.querySelector('#notice').textContent.includes('Connected')")
        page.locator('#participant').fill('SYNTHETIC_THREE_STAGE')
        page.locator('#folder').evaluate("el=>{for(let p=el.parentElement;p;p=p.parentElement)if(p.tagName==='DETAILS')p.open=true;}")
        page.locator('#folder').fill(str(OUT/'data'))
        page.locator('#folder').blur()
        page.wait_for_timeout(1200)
        page.locator('#rehearsal').click()
        page.locator('[data-environment=desktop_fixed_gaze]').click()
        assert page.locator('#stage [aria-pressed=true]').get_attribute('value')==stage
        assert page.locator('#protocol-facts').inner_text().startswith('34')
        assert page.locator('#order').is_hidden()
        page.screenshot(path=str(OUT/(stage+'-desktop.png')),full_page=True)
        page.set_viewport_size({"width":390,"height":844})
        assert page.evaluate('document.documentElement.scrollWidth<=window.innerWidth'),stage+' horizontal overflow'
        page.screenshot(path=str(OUT/(stage+'-mobile.png')),full_page=True)
        page.set_viewport_size({"width":1440,"height":1100})
        page.locator('#start').click()
        page.wait_for_function("document.querySelector('#notice').textContent.includes('Waiting for Unity')")
        active=[r for r in api('/tests/status')['sessions'] if r['state'] in ('prepared','claimed','running')]
        assert len(active)==1
        r=active[0];assert r['config']['comparisonStage']==stage and r['config']['trainingTrials']==34
        (OUT/(stage+'-config.json')).write_text(json.dumps(r['config'],indent=2),encoding='utf-8')
        page.wait_for_function("[...document.querySelectorAll('#stage button')].every(button=>button.disabled)")
        api('/tests/stop',dict(sessionId=r['sessionId']))
        checks.append(dict(stage=stage,budget=34,independentAssignment=True,desktopAndMobile=True))
    # An old backend must never silently allocate the historical 60-trial pair.
    page.route('**/console/health',lambda route:route.fulfill(json={'revision':'evidence-console-v1.31-three-stages30','comparisonRevision':'comparison-three-stages30-v1'}))
    prepare_calls=[]
    page.route('**/tests/prepare',lambda route:(prepare_calls.append(route.request.url),route.fulfill(status=400,json={'error':'Unexpected preparation in compatibility test'})))
    page.goto(BASE+'/tests?stage=fixed_exposure')
    page.wait_for_function("document.querySelector('#next-title').textContent==='Restart the Console service'")
    page.locator('#rehearsal').click()
    assert page.locator('#start').is_disabled()
    page.locator('#setup').dispatch_event('submit')
    page.wait_for_function("document.querySelector('#notice').textContent.includes('Restart the Console service')")
    assert not prepare_calls
    checks.append(dict(oldBackendBlocked=True,noPreparationRequests=True))
    assert not errors,errors
    browser.close()
(OUT/'validation.json').write_text(json.dumps(dict(checks=checks,browserErrors=errors),indent=2),encoding='utf-8')
print(json.dumps(dict(checks=checks,browserErrors=errors)))
