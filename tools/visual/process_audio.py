"""WASAPI process-loopback recorder for a process started by the visual harness.
Uses Microsoft's documented ActivateAudioInterfaceAsync process-loopback API.
Never falls back to a desktop mix or microphone. Optional comtypes/pycaw live in
out/audio-probe. Windows build 20348 or newer is required.
When audible=True, call close() BEFORE terminating the owned process: its original
session mute is restored there. This never changes master/session volume, other
process sessions or endpoint settings. Metadata records the temporary override.
API documentation: https://learn.microsoft.com/en-us/samples/microsoft/windows-classic-samples/applicationloopbackaudio-sample/
"""
import ctypes as C
from ctypes import wintypes as W
import threading,time,wave
from array import array
from pathlib import Path
import comtypes
from comtypes import GUID,IUnknown,COMMETHOD,COMObject
from pycaw.api.audioclient import IAudioClient
from pycaw.pycaw import AudioUtilities
from pycaw.api.audioclient.depend import WAVEFORMATEX

class IAsyncOperation(IUnknown):
    _iid_=GUID('{72A22D78-CDE4-431D-B8CC-843A71199B6D}')
    _methods_=[COMMETHOD([],C.HRESULT,'GetActivateResult',(['out'],C.POINTER(C.HRESULT),'result'),(['out'],C.POINTER(C.POINTER(IUnknown)),'audio'))]
class ICompletion(IUnknown):
    _iid_=GUID('{41D949AB-9862-444A-80F6-C261334DA5EB}')
    _methods_=[COMMETHOD([],C.HRESULT,'ActivateCompleted',(['in'],C.POINTER(IAsyncOperation),'operation'))]
class IAgileObject(IUnknown):
    _iid_=GUID('{94EA2B94-E9CC-49E0-C0FF-EE64CA8F5B90}')
    _methods_=[]
class ICapture(IUnknown):
    _iid_=GUID('{C8ADBD64-E71E-48A0-A4DE-185C395CD317}')
    _methods_=[
        COMMETHOD([],C.HRESULT,'GetBuffer',(['out'],C.POINTER(C.POINTER(C.c_ubyte)),'data'),(['out'],C.POINTER(W.UINT),'frames'),(['out'],C.POINTER(W.DWORD),'flags'),(['out'],C.POINTER(C.c_uint64),'position'),(['out'],C.POINTER(C.c_uint64),'qpc')),
        COMMETHOD([],C.HRESULT,'ReleaseBuffer',(['in'],W.UINT,'frames')),
        COMMETHOD([],C.HRESULT,'GetNextPacketSize',(['out'],C.POINTER(W.UINT),'frames'))]
class Activation(C.Structure):
    _fields_=[('kind',W.DWORD),('pid',W.DWORD),('mode',W.DWORD)]
class Blob(C.Structure):
    _fields_=[('size',W.DWORD),('data',C.c_void_p)]
class Variant(C.Structure):
    _fields_=[('vt',W.WORD),('r1',W.WORD),('r2',W.WORD),('r3',W.WORD),('blob',Blob)]
class Completion(COMObject):
    _com_interfaces_=[ICompletion,IAgileObject]
    def __init__(self): self.ready=threading.Event();self.audio=None;self.error=None
    def ActivateCompleted(self,operation):
        try:
            result,unknown=operation.GetActivateResult()
            if result<0: raise OSError(f'Audio activation failed: 0x{result&0xffffffff:08x}')
            self.audio=unknown.QueryInterface(IAudioClient)
        except Exception as error: self.error=error
        self.ready.set()
        return 0

class ProcessRecording:
    def __init__(self,process,path,audible=False):
        self.process=process;self.path=Path(path).resolve();self.audible=audible;self.session_info=None
        root=Path(__file__).resolve().parents[2]/'out'
        self.path.relative_to(root.resolve()) # recordings are always disposable project artifacts
        if process.poll() is not None: raise RuntimeError('Harness process already exited')
        self.stop=threading.Event();self.error=None;self.started=None;self.frames=0;self.peak=0
        self.thread=threading.Thread(target=self._run,name='harness-process-audio')
        self.thread.start()
    def close(self):
        self.stop.set();self.thread.join(timeout=5)
        if self.thread.is_alive(): raise RuntimeError('Process audio recorder did not stop')
        if self.error: raise self.error
        return dict(pid=self.process.pid,started=self.started,frames=self.frames,rate=44100,channels=2,pcm_peak=self.peak,session=self.session_info,path=str(self.path))
    def _run(self):
        comtypes.CoInitializeEx(0) # callback and capture share the multithreaded apartment
        client=None;event=None;owned_session=None
        kernel=C.WinDLL('kernel32',use_last_error=True)
        kernel.CreateEventW.argtypes=[C.c_void_p,W.BOOL,W.BOOL,W.LPCWSTR];kernel.CreateEventW.restype=W.HANDLE
        kernel.WaitForSingleObject.argtypes=[W.HANDLE,W.DWORD];kernel.WaitForSingleObject.restype=W.DWORD
        kernel.CloseHandle.argtypes=[W.HANDLE]
        try:
            activation=Activation(1,self.process.pid,0) # include only this process tree
            variant=Variant(65,0,0,0,Blob(C.sizeof(activation),C.addressof(activation)))
            callback=Completion();operation=C.POINTER(IAsyncOperation)()
            activate=C.WinDLL('Mmdevapi').ActivateAudioInterfaceAsync
            activate.argtypes=[W.LPCWSTR,C.POINTER(GUID),C.POINTER(Variant),C.POINTER(ICompletion),C.POINTER(C.POINTER(IAsyncOperation))]
            activate.restype=C.HRESULT
            result=activate('VAD\\Process_Loopback',C.byref(IAudioClient._iid_),C.byref(variant),callback.QueryInterface(ICompletion),C.byref(operation))
            if result<0: raise OSError(f'ActivateAudioInterfaceAsync: 0x{result&0xffffffff:08x}')
            if not callback.ready.wait(4): raise TimeoutError('Process-loopback activation timed out')
            if callback.error: raise callback.error
            client=callback.audio
            format=WAVEFORMATEX(1,2,44100,176400,4,16,0)
            client.Initialize(0,0x80060000,0,0,C.byref(format),None)
            capture=client.GetService(C.byref(ICapture._iid_)).QueryInterface(ICapture)
            event=kernel.CreateEventW(None,False,False,None)
            if not event: raise C.WinError(C.get_last_error())
            client.SetEventHandle(event)
            self.path.parent.mkdir(parents=True,exist_ok=True)
            with wave.open(str(self.path),'wb') as output:
                output.setparams((2,2,44100,0,'NONE','not compressed'))
                self.started=time.time();client.Start()
                while not self.stop.is_set():
                    if self.audible and owned_session is None and self.process.poll() is None:
                        session=AudioUtilities.GetProcessSession(self.process.pid)
                        if session is not None and session.ProcessId==self.process.pid:
                            owned_session=session
                            original_mute=session.SimpleAudioVolume.GetMute()
                            self.session_info=dict(pid=session.ProcessId,instance=session.InstanceIdentifier,
                                original_mute=original_mute,original_volume=session.SimpleAudioVolume.GetMasterVolume(),
                                mute_override=bool(original_mute),volume_touched=False,restored=False)
                            # Only this harness-created process-specific session. Never the endpoint,
                            # another process, or the persisted mute setting after this process exits.
                            if original_mute: session.SimpleAudioVolume.SetMute(0,None)
                    kernel.WaitForSingleObject(event,100)
                    while capture.GetNextPacketSize()>0:
                        data,frames,flags,position,qpc=capture.GetBuffer()
                        try:
                            packet=bytes(frames*4) if flags&2 else C.string_at(data,frames*4)
                            self.peak=max(self.peak,max((abs(n) for n in array("h",packet)),default=0))
                            output.writeframesraw(packet)
                            self.frames+=frames
                        finally: capture.ReleaseBuffer(frames)
        except Exception as error: self.error=error
        finally:
            if owned_session is not None and self.session_info is not None:
                try:
                    if self.session_info['mute_override']:
                        if self.process.poll() is not None:
                            raise RuntimeError('Owned process exited before its audio mute could be restored')
                        owned_session.SimpleAudioVolume.SetMute(self.session_info['original_mute'],None)
                    self.session_info['restored']=True
                except Exception as error: self.error=error
            if client:
                try: client.Stop()
                except Exception: pass
            if event: kernel.CloseHandle(event)
            comtypes.CoUninitialize()
