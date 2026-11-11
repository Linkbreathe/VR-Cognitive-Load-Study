"""Read-only browser replay of archived gaze; no POST reaches the study service."""
import json
from pathlib import Path
from playwright.sync_api import sync_playwright

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Evidence/GazeDiagnosis-20261110'
report=json.loads((OUT/'reconstructed-report.json').read_text())
snapshot=json.loads((OUT/'recorded-overview-fixture.json').read_text())
with sync_playwright() as pw:
    browser=pw.chromium.launch(channel='msedge',headless=True)
    page=browser.new_page(viewport=dict(width=1440,height=1100))
    errors=[];page.on('pageerror',lambda e:errors.append(str(e)))
    def route(request):
        if request.request.method!='GET':return request.abort()
        if '/console/snapshot' in request.request.url:return request.fulfill(json=snapshot)
        if '/tests/comparison-report' in request.request.url:return request.fulfill(json=report)
        return request.continue_()
    page.route('**/*',route)
    page.goto('http://127.0.0.1:8765/#physiology')
    page.locator('#gaze-current-point').wait_for()
    assert page.locator('.gaze-history-dot').count()>0
    assert 'plot expanded' in page.locator('#gaze-coverage').inner_text()
    assert page.locator('#gaze-task').inner_text()=='0%'
    page.locator('.ec-gaze-panel h2').evaluate("e=>e.textContent='Recorded gaze replay · display verification'")
    page.locator('.ec-gaze-panel').screenshot(path=str(OUT/'expanded-overview.png'))
    page.evaluate("""report=>{
      const box=document.createElement('div');box.id='gaze-replay-review';document.body.prepend(box);
      box.style.cssText='background:#fff;padding:24px;max-width:1100px;margin:auto';
      window.EvidenceUI.render(box,report.checkpoints.find(c=>c.trial===14),report);
    }""",report)
    review=page.locator('#gaze-replay-review')
    assert 'Current-state review window: 5 scored trials' in review.inner_text()
    assert 'Plot expanded to show recorded positions' in review.inner_text()
    assert review.locator('.de-heatmap rect[rx="4"]').count()>0
    review.locator('.de-gaze').screenshot(path=str(OUT/'expanded-decision.png'))
    for width in [1440,768,390]:
        page.set_viewport_size(dict(width=width,height=1100))
        assert page.evaluate('document.documentElement.scrollWidth<=innerWidth'),width
    assert not errors,errors
    (OUT/'browser-validation.json').write_text(json.dumps(dict(result='PASS',pageErrors=errors,widths=[1440,768,390],kind='read-only archived replay',taskShareStillZero=True),indent=2))
    browser.close()
print('PASS: recorded overview and decision gaze visible, five-trial window explicit, task share unchanged, no page errors.')
