"""Offline filter-edge and 50 Hz sensitivity; same retained windows, no artifact rejection."""
import json
from pathlib import Path
import sys
import numpy as np
from scipy import signal

sys.path.insert(0,str(Path(__file__).resolve().parent))
from analyze_p002_raw_eeg import ROOT, OUT, SIDS, SPECTRA, extract, fit_predict, auc
from personalized_signals import sampled_window
import workload_rf as w


def spectrum(x, notch=False):
    if notch:
        b,a=signal.iirnotch(50,30,fs=250)
        x=signal.filtfilt(b,a,x,axis=0)
    y=signal.sosfiltfilt(signal.butter(5,[2,30],fs=250,btype='bandpass',output='sos'),x,axis=0)
    return y


def psd(y):
    return signal.periodogram(y.reshape(5,250,8),fs=250,window='hann',axis=1)[1][:,:16,:]


def main():
    synthetic=[]
    t=np.arange(1250)/250
    for amplitude in (0,1000,2800):
        # Known 50 Hz sine only: no true theta or alpha component.
        x=np.repeat((amplitude*np.sin(2*np.pi*50*t))[:,None],8,axis=1)
        for notch in (False,True):
            f=w.features(psd(spectrum(x,notch)),10)
            synthetic.append(dict(lineAmplitude=amplitude,notch=notch,alphaPerEpoch=f[:,7].tolist(),
                                  thetaPerEpoch=f[:,0].tolist()))
    variants={key:[] for key in ('reconstructed','notch50','context_padding')};agreement=[];edge=[]
    for n,(sid,specpath) in enumerate(zip(SIDS,SPECTRA)):
        src=Path('D:/ExperimentData/PatentDemo/P002/sessions')/sid/'service'
        raw=[json.loads(s) for s in (src/'raw_signals.jsonl').read_bytes().splitlines()]
        times=np.concatenate([r['corrected_monotonic'] for r in raw]);samples=np.concatenate([r['samples'] for r in raw])
        packets=[json.loads(s)['packet'] for s in (src/'telemetry.jsonl').read_text(encoding='utf-8').splitlines()]
        folder=specpath.parent
        windows=[json.loads(s) for s in (folder/'service/load_windows.jsonl').read_text().splitlines()]
        original=np.load(specpath)
        local={key:{stage:np.full((36,5,16,8),np.nan) for stage in w.STAGES} for key in variants}
        discrepancies=[];skipped=0;boundaries=0
        for stage in w.STAGES:
            onset=max(r['onset'] for r in windows if r['stage']==stage)
            selected=[r for r in windows if r['stage']==stage and r['onset']==onset and r['selected']]
            counters=[0,0,0]
            for row in selected:
                fold=row['startSecond']//60;slot=fold*12+counters[fold];counters[fold]+=1
                start=row['start'];packet=next(p for p in packets if p['now']>=start+6)
                offset=packet['clockOffset']
                x,reason=sampled_window(times-offset,samples,start,start+5,250)
                if x is None: raise ValueError(reason)
                x=x[:1250];base=psd(spectrum(x));local['reconstructed'][stage][slot]=base
                discrepancies.append(float(np.max(np.abs(base-original[stage][slot]))))
                local['notch50'][stage][slot]=psd(spectrum(x,True))
                # Filter using two seconds of real context on both sides; only central 5 s scored.
                if not 2 <= row['startSecond'] % 60 <= 53:
                    boundaries+=1
                    continue
                padded,reason=sampled_window(times-offset,samples,start-2,start+7,250)
                if padded is not None:
                    local['context_padding'][stage][slot]=psd(spectrum(padded[:2250])[500:1750])
                else:skipped+=1
            for key in local:
                good=[v for v in local[key][stage] if np.isfinite(v).all()]
                features=np.array([w.features(v,10) for v in good])
                edge.append(dict(recording=n+1,stage=stage,method=key,windows=len(good),
                                 medianEpochFeatures=np.median(features,axis=0).tolist()))
        for key in variants:variants[key].append(local[key])
        agreement.append(dict(recording=n+1,maxSpectrumAbsoluteDifference=max(discrepancies),
                              contextUnavailableWindows=skipped,contextFoldBoundaryExclusions=boundaries))
    variants['reconstructed_context_subset']=[{stage:np.where(np.isfinite(context[stage]),base[stage],np.nan)
                                              for stage in w.STAGES}
                                             for context,base in zip(variants['context_padding'],variants['reconstructed'])]
    # Evaluate context only on windows present in both methods; other methods retain all windows.
    evaluations=[]
    for key,zlist in variants.items():
        datasets=[extract(z) for z in zlist]
        for source in (0,1):
            windows,y,slots=datasets[source];other,other_y,_=datasets[1-source];folds=[]
            for fold in range(3):
                mask=slots//12==fold
                scores=fit_predict([v for v,k in zip(windows,mask) if not k],y[~mask],[v for v,k in zip(windows,mask) if k])
                folds.append(auc(y[mask],scores))
            evaluations.append(dict(method=key,source=source+1,target=2-source,
                                    sourceWindows=len(y),targetWindows=len(other_y),
                                    withinMeanAuc=float(np.mean(folds)),folds=folds,
                                    crossSessionAuc=auc(other_y,fit_predict(windows,y,other))))
    result=dict(purpose='Engineering sensitivity, not model selection; no production changes',syntheticLineOnly=synthetic,
                reconstruction=agreement,edgeEpochs=edge,evaluation=evaluations,
                contextPolicy='Two seconds before/after; windows near fold/stage boundaries and missing context excluded. Compare with reconstructed_context_subset on the same windows; no context enters a held-out fold.')
    (OUT/'filter-sensitivity.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
    print(json.dumps(dict(synthetic=synthetic,reconstruction=agreement,evaluation=evaluations),indent=2))


if __name__=='__main__': main()
