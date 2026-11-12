"""Inspect real report layouts using explicitly synthetic read-only browser fixtures."""
from pathlib import Path
import json
from playwright.sync_api import sync_playwright
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Evidence/Comparison34-20261112/browser'
with sync_playwright() as pw:
    browser=pw.chromium.launch(channel='msedge',headless=True)
    page=browser.new_page(viewport={'width':1440,'height':1100})
    errors=[];page.on('pageerror',lambda error:errors.append(str(error)))
    active=[None]
    page.route('**/tests/comparison-report?*',lambda route:route.fulfill(json=active[0]))
    for stage in ('fixed_exposure','fixed-candidate','layer_a','layer_b'):
        active[0]=json.loads((OUT/(stage+'-report.json')).read_text(encoding='utf-8'))
        page.goto('http://127.0.0.1:8773/comparison?session=SYNTHETIC_UI_ONLY')
        page.wait_for_function("document.querySelector('#status').textContent.includes('completed trials')")
        assert '34 completed trials' in page.locator('#status').inner_text()
        if stage.startswith('fixed'):
            assert page.locator('#fixed-thresholds').is_visible()
            assert page.locator('#decisions').is_hidden()
            assert ('Suggested ON' if stage=='fixed-candidate' else 'No suggested threshold pair') in page.locator('#fixed-thresholds').inner_text()
            if stage=='fixed-candidate':
                assert 'Trials 19–34' in page.locator('#fixed-thresholds').inner_text()
                assert page.locator('.ft-point').count()>0
        else:
            assert page.locator('#arm-filter option').count()==1
            assert page.locator('#totals thead th').count()==2
        page.screenshot(path=str(OUT/(stage+'-report-desktop.png')),full_page=True)
        page.set_viewport_size({'width':390,'height':844})
        assert page.evaluate('document.documentElement.scrollWidth<=window.innerWidth'),stage+' overflow'
        page.screenshot(path=str(OUT/(stage+'-report-mobile.png')),full_page=True)
        page.set_viewport_size({'width':1440,'height':1100})
    assert not errors,errors
    browser.close()
(OUT/'report-validation.json').write_text(json.dumps(dict(fixtures=4,viewports=2,browserErrors=errors)),encoding='utf-8')
print('PASS: four report fixtures, desktop/mobile, candidate and insufficient-data states, single-stage A/B reports; no browser errors.')
