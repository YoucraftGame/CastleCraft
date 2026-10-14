// CastleCraft GPU overlay transport. Original implementation; Windows x64 / D3D11 / WGL_NV_DX_interop2.
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <d3d11.h>
#include <dxgi.h>
#include <dxgi1_2.h>
#include <GL/gl.h>
#include <mutex>
#include <cstdint>
#include <cstring>
#define API extern "C" __declspec(dllexport)
template<class T> void drop(T*& p){if(p){p->Release();p=nullptr;}}
struct alignas(8) Wire {
 LONG seq; DWORD magic,abi,hostPid; uint64_t session; LUID adapter;
 uint64_t handle,generation,frame,consumed; DWORD width,height; uint64_t hostBeat,guestBeat,copyBeat,copyGeneration,producerPid,backend;
};
static_assert(sizeof(Wire)==120,"wire ABI");
static HANDLE mapping;static Wire* wire;
static std::mutex guard;static ID3D11Device* hostDevice;static ID3D11DeviceContext* hostContext;
static ID3D11Texture2D* destination;static ID3D11Texture2D* incoming;static IDXGIKeyedMutex* incomingMutex;
static uint64_t openedGeneration,openedHandle;static LONG errorCode,lastHResult;
static bool mapped(){
 if(wire)return true;
 // Isolated regression probes use their own session and never touch a live game.
 wchar_t name[256];DWORD length=GetEnvironmentVariableW(L"CASTLECRAFT_GPU_MAPPING",name,256);
 if(length>=256)return false;
 mapping=CreateFileMappingW(INVALID_HANDLE_VALUE,nullptr,PAGE_READWRITE,0,sizeof(Wire),length?name:L"Local\\CastleCraft_gpu_manual_v2");
 if(!mapping)return false;wire=(Wire*)MapViewOfFile(mapping,FILE_MAP_ALL_ACCESS,0,0,sizeof(Wire));return wire!=nullptr;
}
static bool snapshot(Wire& out){if(!wire)return false;for(int n=0;n<3;n++){LONG s=InterlockedCompareExchange(&wire->seq,0,0);if(s&1)continue;MemoryBarrier();memcpy(&out,wire,sizeof out);MemoryBarrier();if(s==wire->seq)return out.magic==0x43434750&&out.abi==2;}return false;}
static void clearIncoming(){drop(incomingMutex);drop(incoming);openedGeneration=openedHandle=0;}
API int __cdecl CC_HostInit(void* nativeTexture){
 std::lock_guard<std::mutex> lock(guard);if(!nativeTexture||!mapped())return 0;
 if(hostDevice)return 1;
 auto texture=(ID3D11Texture2D*)nativeTexture;texture->GetDevice(&hostDevice);if(!hostDevice)return 0;
 IDXGIDevice* device=nullptr;IDXGIAdapter* adapter=nullptr;DXGI_ADAPTER_DESC desc{};
 HRESULT hr=hostDevice->QueryInterface(__uuidof(IDXGIDevice),(void**)&device);
 if(SUCCEEDED(hr))hr=device->GetAdapter(&adapter);if(SUCCEEDED(hr))hr=adapter->GetDesc(&desc);drop(adapter);drop(device);
 if(FAILED(hr)){drop(hostDevice);return 0;}
 hostDevice->GetImmediateContext(&hostContext);
 // A single manual host. Never attach to another live consumer's device.
 if(wire->hostPid&&wire->hostPid!=GetCurrentProcessId()&&GetTickCount64()-wire->hostBeat<2000){drop(hostContext);drop(hostDevice);return 0;}
 memset(wire,0,sizeof *wire);wire->magic=0x43434750;wire->abi=2;wire->hostPid=GetCurrentProcessId();
 wire->session=(GetTickCount64()<<16)^GetCurrentProcessId();wire->adapter=desc.AdapterLuid;wire->hostBeat=GetTickCount64();return 1;
}
API int __cdecl CC_HostInfo(int* width,int* height){
 std::lock_guard<std::mutex> lock(guard);if(!hostDevice)return 0;wire->hostBeat=GetTickCount64();Wire state{};
 if(!snapshot(state)||!state.handle||GetTickCount64()-state.guestBeat>1000||state.width<1||state.height<1||state.width>3840||state.height>2160)return 0;
 *width=(int)state.width;*height=(int)state.height;return 1;
}
API int __cdecl CC_HostBind(void* texture){
 std::lock_guard<std::mutex> lock(guard);if(!hostDevice)return 0;
 auto next=(ID3D11Texture2D*)texture;if(next)next->AddRef();drop(destination);destination=next;return 1;
}
static void __stdcall renderEvent(int id){
 if(id!=1)return;std::lock_guard<std::mutex> lock(guard);if(!hostDevice||!destination)return;Wire state{};
 if(!snapshot(state)||!state.handle||!state.frame||(state.frame==state.consumed&&state.copyGeneration==state.generation)||GetTickCount64()-state.guestBeat>1000)return;
 if(openedGeneration!=state.generation||openedHandle!=state.handle){
  clearIncoming();HRESULT hr=hostDevice->OpenSharedResource((HANDLE)(uintptr_t)state.handle,__uuidof(ID3D11Texture2D),(void**)&incoming);
  if(FAILED(hr)){errorCode=10;return;}hr=incoming->QueryInterface(__uuidof(IDXGIKeyedMutex),(void**)&incomingMutex);
  if(FAILED(hr)){clearIncoming();errorCode=11;return;}openedGeneration=state.generation;openedHandle=state.handle;
 }
 D3D11_TEXTURE2D_DESC dest{},src{};destination->GetDesc(&dest);incoming->GetDesc(&src);
 if(dest.Width!=src.Width||dest.Height!=src.Height||dest.Format!=src.Format||dest.SampleDesc.Count!=1){errorCode=12;return;}
 HRESULT acquired=incomingMutex->AcquireSync(1,0);if(acquired!=S_OK){if(acquired==WAIT_ABANDONED){clearIncoming();errorCode=13;}return;}
 hostContext->CopyResource(destination,incoming);hostContext->Flush();incomingMutex->ReleaseSync(0);
 wire->consumed=state.frame;wire->copyGeneration=state.generation;wire->copyBeat=GetTickCount64();
}
API void* __cdecl CC_RenderEvent(){return (void*)&renderEvent;}
API int __cdecl CC_HostReady(){return wire&&wire->consumed&&wire->copyGeneration==wire->generation&&GetTickCount64()-wire->copyBeat<250;}
API int __cdecl CC_HostTopDown(){return 0;} // Minecraft main-target rows stay bottom-up.
API int __cdecl CC_Error(){return errorCode;}
API int __cdecl CC_HResult(){return lastHResult;}
API void __cdecl CC_HostClose(){std::lock_guard<std::mutex> lock(guard);if(wire&&wire->hostPid==GetCurrentProcessId()){wire->hostBeat=0;wire->hostPid=0;}clearIncoming();drop(destination);drop(hostContext);drop(hostDevice);}

// GL objects stay on Minecraft's render thread. Register a LOCAL D3D texture with WGL;
// keyed-mutex ownership is only used on the separate cross-process shared texture.
using OpenDevice=HANDLE(WINAPI*)(void*);using CloseDevice=BOOL(WINAPI*)(HANDLE);
using RegisterObject=HANDLE(WINAPI*)(HANDLE,void*,GLuint,GLenum,GLenum);using UnregisterObject=BOOL(WINAPI*)(HANDLE,HANDLE);
using LockObjects=BOOL(WINAPI*)(HANDLE,GLint,HANDLE*);
using GenFbo=void(APIENTRY*)(GLsizei,GLuint*);using BindFbo=void(APIENTRY*)(GLenum,GLuint);
using Attach=void(APIENTRY*)(GLenum,GLenum,GLenum,GLuint,GLint);using CheckFbo=GLenum(APIENTRY*)(GLenum);
using Blit=void(APIENTRY*)(GLint,GLint,GLint,GLint,GLint,GLint,GLint,GLint,GLbitfield,GLenum);
static OpenDevice dxOpen;static CloseDevice dxClose;static RegisterObject dxRegister;static UnregisterObject dxUnregister;
static LockObjects dxLock,dxUnlock;static GenFbo genFbo,deleteFbo;static BindFbo bindFbo;static Attach attach;static CheckFbo checkFbo;static Blit blit;
static ID3D11Device* producer;static ID3D11DeviceContext* producerContext;
static ID3D11Texture2D* localTexture;static ID3D11Texture2D* sharedTexture;static IDXGIKeyedMutex* sharedMutex;
static HANDLE glDevice,glObject;static GLuint glTexture,readFbo,writeFbo;static uint64_t session,generation;static int width,height;
static HGLRC glContext;
static uint64_t nextAttempt;
static void* proc(const char* name){auto p=wglGetProcAddress(name);if(!p||p==(PROC)1||p==(PROC)2||p==(PROC)3||p==(PROC)-1)return nullptr;return (void*)p;}
static bool loadGL(){
 dxOpen=(OpenDevice)proc("wglDXOpenDeviceNV");dxClose=(CloseDevice)proc("wglDXCloseDeviceNV");dxRegister=(RegisterObject)proc("wglDXRegisterObjectNV");dxUnregister=(UnregisterObject)proc("wglDXUnregisterObjectNV");dxLock=(LockObjects)proc("wglDXLockObjectsNV");dxUnlock=(LockObjects)proc("wglDXUnlockObjectsNV");
 genFbo=(GenFbo)proc("glGenFramebuffers");deleteFbo=(GenFbo)proc("glDeleteFramebuffers");bindFbo=(BindFbo)proc("glBindFramebuffer");attach=(Attach)proc("glFramebufferTexture2D");checkFbo=(CheckFbo)proc("glCheckFramebufferStatus");blit=(Blit)proc("glBlitFramebuffer");
 return dxOpen&&dxClose&&dxRegister&&dxUnregister&&dxLock&&dxUnlock&&genFbo&&deleteFbo&&bindFbo&&attach&&checkFbo&&blit;
}
static void clearProducer(){
 if(glObject&&dxUnregister)dxUnregister(glDevice,glObject);glObject=nullptr;
 if(glTexture)glDeleteTextures(1,&glTexture);glTexture=0;
 if(readFbo&&deleteFbo)deleteFbo(1,&readFbo);if(writeFbo&&deleteFbo)deleteFbo(1,&writeFbo);readFbo=writeFbo=0;
 if(glDevice&&dxClose)dxClose(glDevice);glDevice=nullptr;
 drop(sharedMutex);drop(sharedTexture);drop(localTexture);drop(producerContext);drop(producer);width=height=0;
}
static bool createProducer(const Wire& state,int w,int h){
 clearProducer();if(!loadGL()){errorCode=20;return false;}
 IDXGIFactory1* factory=nullptr;IDXGIAdapter1* selected=nullptr;
 if(FAILED(CreateDXGIFactory1(__uuidof(IDXGIFactory1),(void**)&factory)))return false;
 for(UINT i=0;;i++){IDXGIAdapter1* candidate=nullptr;if(factory->EnumAdapters1(i,&candidate)==DXGI_ERROR_NOT_FOUND)break;DXGI_ADAPTER_DESC desc{};candidate->GetDesc(&desc);if(desc.AdapterLuid.LowPart==state.adapter.LowPart&&desc.AdapterLuid.HighPart==state.adapter.HighPart){selected=candidate;break;}drop(candidate);}
 drop(factory);if(!selected){errorCode=21;return false;}
 HRESULT hr=D3D11CreateDevice(selected,D3D_DRIVER_TYPE_UNKNOWN,nullptr,0,nullptr,0,D3D11_SDK_VERSION,&producer,nullptr,&producerContext);drop(selected);if(FAILED(hr)){errorCode=22;return false;}
 D3D11_TEXTURE2D_DESC desc{};desc.Width=w;desc.Height=h;desc.MipLevels=desc.ArraySize=1;desc.Format=DXGI_FORMAT_R8G8B8A8_UNORM;desc.SampleDesc.Count=1;desc.Usage=D3D11_USAGE_DEFAULT;desc.BindFlags=D3D11_BIND_RENDER_TARGET|D3D11_BIND_SHADER_RESOURCE;
 if(FAILED(producer->CreateTexture2D(&desc,nullptr,&localTexture)))return false;
 desc.MiscFlags=D3D11_RESOURCE_MISC_SHARED_KEYEDMUTEX;
 if(FAILED(producer->CreateTexture2D(&desc,nullptr,&sharedTexture)))return false;
 if(FAILED(sharedTexture->QueryInterface(__uuidof(IDXGIKeyedMutex),(void**)&sharedMutex)))return false;
 IDXGIResource* resource=nullptr;HANDLE handle=nullptr;
 hr=sharedTexture->QueryInterface(__uuidof(IDXGIResource),(void**)&resource);if(SUCCEEDED(hr))hr=resource->GetSharedHandle(&handle);drop(resource);if(FAILED(hr))return false;
 glDevice=dxOpen(producer);if(!glDevice){errorCode=23;return false;}
 glGenTextures(1,&glTexture);glObject=dxRegister(glDevice,localTexture,glTexture,GL_TEXTURE_2D,0x0001);if(!glObject){errorCode=24;return false;}
 genFbo(1,&readFbo);genFbo(1,&writeFbo);width=w;height=h;session=state.session;generation=state.generation+1;
 InterlockedIncrement(&wire->seq);wire->frame=0;wire->handle=(uint64_t)(uintptr_t)handle;wire->width=w;wire->height=h;wire->generation=generation;wire->backend=1;MemoryBarrier();InterlockedIncrement(&wire->seq);return true;
}
API int __cdecl CC_Produce(int sourceTexture,int w,int h){
 if(!wglGetCurrentContext()||!sourceTexture||w<1||h<1||w>3840||h>2160||!mapped())return 0;
 // Never operate stale GL names in a different context; fall back instead.
 if(glContext&&glContext!=wglGetCurrentContext()){errorCode=25;return 0;}glContext=wglGetCurrentContext();
 Wire state{};if(!snapshot(state)||!state.hostPid||GetTickCount64()-state.hostBeat>1000)return 0;
 if(state.producerPid&&state.producerPid!=GetCurrentProcessId()){
  HANDLE other=OpenProcess(SYNCHRONIZE,FALSE,(DWORD)state.producerPid);
  if(other){bool alive=WaitForSingleObject(other,0)==WAIT_TIMEOUT;CloseHandle(other);if(alive)return 0;}
  else if(GetLastError()!=ERROR_INVALID_PARAMETER)return 0;
 }
 auto owner=(LONG64)state.producerPid;
 if(InterlockedCompareExchange64((volatile LONG64*)&wire->producerPid,GetCurrentProcessId(),owner)!=owner)return 0;
 if(!producer||session!=state.session||width!=w||height!=h){if(GetTickCount64()<nextAttempt)return 0;if(!createProducer(state,w,h)){clearProducer();nextAttempt=GetTickCount64()+2000;return 0;}}
 HRESULT acquired=sharedMutex->AcquireSync(0,0);
 if(acquired!=S_OK){if(acquired==WAIT_ABANDONED){clearProducer();errorCode=26;return 0;}return CC_HostReady();}
 if(!dxLock(glDevice,1,&glObject)){sharedMutex->ReleaseSync(0);errorCode=27;clearProducer();nextAttempt=GetTickCount64()+2000;return 0;}
 GLint oldRead=0,oldDraw=0;glGetIntegerv(0x8CAA,&oldRead);glGetIntegerv(0x8CA6,&oldDraw);
 bindFbo(0x8CA8,readFbo);attach(0x8CA8,0x8CE0,GL_TEXTURE_2D,(GLuint)sourceTexture,0);
 bindFbo(0x8CA9,writeFbo);attach(0x8CA9,0x8CE0,GL_TEXTURE_2D,glTexture,0);
 bool valid=checkFbo(0x8CA8)==0x8CD5&&checkFbo(0x8CA9)==0x8CD5;
 GLboolean scissor=glIsEnabled(GL_SCISSOR_TEST);if(scissor)glDisable(GL_SCISSOR_TEST);
 if(valid)blit(0,0,w,h,0,0,w,h,GL_COLOR_BUFFER_BIT,GL_NEAREST);
 if(scissor)glEnable(GL_SCISSOR_TEST);
 bindFbo(0x8CA8,(GLuint)oldRead);bindFbo(0x8CA9,(GLuint)oldDraw);
 bool unlocked=dxUnlock(glDevice,1,&glObject)!=FALSE;
 if(!valid||!unlocked){sharedMutex->ReleaseSync(0);errorCode=28;clearProducer();nextAttempt=GetTickCount64()+2000;return 0;}
 producerContext->CopyResource(sharedTexture,localTexture);producerContext->Flush();sharedMutex->ReleaseSync(1);
 InterlockedIncrement(&wire->seq);wire->frame++;wire->guestBeat=GetTickCount64();MemoryBarrier();InterlockedIncrement(&wire->seq);
 return CC_HostReady();
}
API int __cdecl CC_Abi(){return 2;}
API void __cdecl CC_ProducerClose(){if(glContext==wglGetCurrentContext())clearProducer();}

// Vulkan imports this D3D11 texture directly. No WGL context and no producer-side pixel readback.
API int __cdecl CC_VulkanPrepare(int w,int h,int luidLow,int luidHigh){
 if(w<1||h<1||w>3840||h>2160||!mapped())return 0;Wire state{};
 if(!snapshot(state)||!state.hostPid||GetTickCount64()-state.hostBeat>1000)return 0;
 if(state.adapter.LowPart!=(DWORD)luidLow||state.adapter.HighPart!=(LONG)luidHigh){errorCode=50;return 0;}
 if(state.producerPid&&state.producerPid!=GetCurrentProcessId()){
  HANDLE other=OpenProcess(SYNCHRONIZE,FALSE,(DWORD)state.producerPid);
  if(other){bool alive=WaitForSingleObject(other,0)==WAIT_TIMEOUT;CloseHandle(other);if(alive)return 0;}
  else if(GetLastError()!=ERROR_INVALID_PARAMETER)return 0;
 }
 auto owner=(LONG64)state.producerPid;
 if(InterlockedCompareExchange64((volatile LONG64*)&wire->producerPid,GetCurrentProcessId(),owner)!=owner)return 0;
 if(producer&&session==state.session&&width==w&&height==h&&state.backend==2)return 1;
 if(GetTickCount64()<nextAttempt)return 0;clearProducer();
 IDXGIFactory1* factory=nullptr;IDXGIAdapter1* selected=nullptr;
 if(FAILED(CreateDXGIFactory1(__uuidof(IDXGIFactory1),(void**)&factory)))return 0;
 for(UINT i=0;;i++){IDXGIAdapter1* candidate=nullptr;if(factory->EnumAdapters1(i,&candidate)==DXGI_ERROR_NOT_FOUND)break;DXGI_ADAPTER_DESC desc{};candidate->GetDesc(&desc);if(desc.AdapterLuid.LowPart==state.adapter.LowPart&&desc.AdapterLuid.HighPart==state.adapter.HighPart){selected=candidate;break;}drop(candidate);}
 drop(factory);if(!selected){errorCode=51;return 0;}
 HRESULT hr=D3D11CreateDevice(selected,D3D_DRIVER_TYPE_UNKNOWN,nullptr,0,nullptr,0,D3D11_SDK_VERSION,&producer,nullptr,&producerContext);drop(selected);
 if(FAILED(hr)){errorCode=52;nextAttempt=GetTickCount64()+2000;return 0;}
 D3D11_TEXTURE2D_DESC desc{};desc.Width=w;desc.Height=h;desc.MipLevels=desc.ArraySize=1;desc.Format=DXGI_FORMAT_R8G8B8A8_UNORM;desc.SampleDesc.Count=1;desc.Usage=D3D11_USAGE_DEFAULT;desc.BindFlags=D3D11_BIND_RENDER_TARGET|D3D11_BIND_SHADER_RESOURCE;desc.MiscFlags=D3D11_RESOURCE_MISC_SHARED_KEYEDMUTEX;
 hr=producer->CreateTexture2D(&desc,nullptr,&sharedTexture);if(FAILED(hr)){lastHResult=hr;errorCode=54;clearProducer();nextAttempt=GetTickCount64()+2000;return 0;}
 IDXGIResource* resource=nullptr;HANDLE handle=nullptr;
 if(SUCCEEDED(hr))hr=sharedTexture->QueryInterface(__uuidof(IDXGIResource),(void**)&resource);if(FAILED(hr)){lastHResult=hr;errorCode=55;clearProducer();nextAttempt=GetTickCount64()+2000;return 0;}
 if(SUCCEEDED(hr))hr=resource->GetSharedHandle(&handle);drop(resource);
 if(FAILED(hr)){lastHResult=hr;clearProducer();errorCode=53;nextAttempt=GetTickCount64()+2000;return 0;}
 width=w;height=h;session=state.session;generation=state.generation+1;
 InterlockedIncrement(&wire->seq);wire->frame=0;wire->handle=(uint64_t)(uintptr_t)handle;wire->width=w;wire->height=h;wire->generation=generation;wire->backend=2;MemoryBarrier();InterlockedIncrement(&wire->seq);return 1;
}
API uint64_t __cdecl CC_VulkanHandle(){return wire&&wire->producerPid==GetCurrentProcessId()?wire->handle:0;}
API uint64_t __cdecl CC_VulkanGeneration(){return wire?wire->generation:0;}
API int __cdecl CC_VulkanCanSubmit(uint64_t gen){
 // A producer waiting for consumption is still alive. Otherwise a delayed first
 // consumer frame expires guestBeat and HostInfo stops scheduling the very copy
 // needed to release it. Do not forge consumption or submit over an owned image.
 if(wire&&wire->backend==2&&wire->generation==gen&&wire->producerPid==GetCurrentProcessId())wire->guestBeat=GetTickCount64();
 return wire&&wire->backend==2&&wire->generation==gen&&wire->producerPid==GetCurrentProcessId()&&GetTickCount64()-wire->hostBeat<1000&&(!wire->frame||(wire->copyGeneration==gen&&wire->consumed==wire->frame));
}
API int __cdecl CC_VulkanPublish(uint64_t gen){
 if(!wire||wire->generation!=gen||wire->producerPid!=GetCurrentProcessId())return 0;
 InterlockedIncrement(&wire->seq);wire->frame++;wire->guestBeat=GetTickCount64();MemoryBarrier();InterlockedIncrement(&wire->seq);return CC_HostReady();
}
