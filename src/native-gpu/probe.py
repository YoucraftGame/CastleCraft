"""Headless ABI/fallback check only. Does not launch/attach either game or claim GPU activation."""
from pathlib import Path
import ctypes,json
root=Path(__file__).resolve().parents[2]
lib=ctypes.CDLL(str(root/'build/CastleCraft.GpuBridge.dll'))
lib.CC_Abi.restype=ctypes.c_int
lib.CC_Produce.argtypes=[ctypes.c_int]*3
lib.CC_HostInfo.argtypes=[ctypes.POINTER(ctypes.c_int)]*2
lib.CC_HostInit.argtypes=[ctypes.c_void_p]
checks={
 'abi_v2':lib.CC_Abi()==2,
 'no_context_cpu_fallback':lib.CC_Produce(1,1920,1080)==0,
 'invalid_texture_cpu_fallback':lib.CC_Produce(0,1920,1080)==0,
 'no_host_no_descriptor':lib.CC_HostInfo(ctypes.byref(ctypes.c_int()),ctypes.byref(ctypes.c_int()))==0,
 'null_host_device_rejected':lib.CC_HostInit(None)==0,
}
assert all(checks.values()),checks
result={'checks':checks,'passed':len(checks),'gpu_runtime_verified':False}
(root/'build/gpu-native-probe.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print('NATIVE GPU ABI/FALLBACK PASS:',len(checks),'(no live GPU-sharing acceptance claimed)')
