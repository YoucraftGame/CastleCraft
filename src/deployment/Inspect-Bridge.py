"""Read-only verification of the exact manual bridge header. Never launch or control games."""
import ctypes,struct,json,datetime
from pathlib import Path
def main():
 k=ctypes.WinDLL("kernel32",use_last_error=True)
 k.OpenFileMappingW.argtypes=[ctypes.c_uint,ctypes.c_int,ctypes.c_wchar_p];k.OpenFileMappingW.restype=ctypes.c_void_p
 k.MapViewOfFile.argtypes=[ctypes.c_void_p,ctypes.c_uint,ctypes.c_uint,ctypes.c_uint,ctypes.c_size_t];k.MapViewOfFile.restype=ctypes.c_void_p
 k.GetTickCount64.restype=ctypes.c_ulonglong
 k.UnmapViewOfFile.argtypes=[ctypes.c_void_p];k.CloseHandle.argtypes=[ctypes.c_void_p]
 result={"checked_at":datetime.datetime.now(datetime.timezone(datetime.timedelta(hours=8))).isoformat(),"mapping":"Local\\CastleCraft_v3_manual","read_only":True}
 h=k.OpenFileMappingW(4,False,result["mapping"])
 if not h:result.update({"mapping_present":False,"error":ctypes.get_last_error()})
 else:
  v=k.MapViewOfFile(h,4,0,0,4096)
  try:
   if not v:result.update({"mapping_present":False,"error":ctypes.get_last_error()})
   else:
    a=struct.unpack("<IIIIQQ",ctypes.string_at(v,32));now=k.GetTickCount64()
    result.update({"mapping_present":True,"magic":hex(a[0]),"protocol":a[1],"host_pid":a[2],"guest_pid":a[3],"host_age_ms":now-a[4],"guest_age_ms":now-a[5],"host_live":a[2]!=0 and now-a[4]<8000,"guest_live":a[3]!=0 and now-a[5]<8000,"layout_valid":a[0]==0x43594B53 and a[1]==11})
  finally:
   if v:k.UnmapViewOfFile(v)
   k.CloseHandle(h)
 root=Path(__file__).resolve().parents[2]
 (root/"build/loader-live-verification.json").write_text(json.dumps(result,indent=2,ensure_ascii=False)+"\n",encoding="utf-8")
 print(json.dumps(result,ensure_ascii=False))
if __name__=="__main__":main()
