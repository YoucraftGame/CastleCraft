"""Headless real-GPU smoke test: Vulkan clear -> shared D3D11 texture -> native consumer.
No game is started/attached. CPU pixel readback here is verification ONLY, not transport.
"""
import ctypes as c,json,time,sys,subprocess,threading,hashlib
from pathlib import Path
U=c.c_uint32;Q=c.c_uint64;P=c.c_void_p;I=c.c_int32
def struct(name,fields):return type(name,(c.Structure,),{'_fields_':fields})
App=struct('App',[('s',U),('n',P),('name',c.c_char_p),('v',U),('engine',c.c_char_p),('ev',U),('api',U)])
Instance=struct('Instance',[('s',U),('n',P),('flags',U),('app',c.POINTER(App)),('lc',U),('layers',P),('ec',U),('ext',P)])
QueueInfo=struct('QueueInfo',[('s',U),('n',P),('flags',U),('family',U),('count',U),('priority',P)])
DeviceInfo=struct('DeviceInfo',[('s',U),('n',P),('flags',U),('qc',U),('queues',P),('lc',U),('layers',P),('ec',U),('ext',P),('features',P)])
QueueProperties=struct('QueueProperties',[('flags',U),('count',U),('timestamp',U),('x',U),('y',U),('z',U)])
External=struct('External',[('s',U),('n',P),('types',U)])
ImageInfo=struct('ImageInfo',[('s',U),('n',P),('flags',U),('type',U),('format',U),('w',U),('h',U),('d',U),('mips',U),('layers',U),('samples',U),('tiling',U),('usage',U),('sharing',U),('qc',U),('families',P),('layout',U)])
Requirement=struct('Requirement',[('size',Q),('alignment',Q),('bits',U)])
HandleProperties=struct('HandleProperties',[('s',U),('n',P),('bits',U)])
Dedicated=struct('Dedicated',[('s',U),('n',P),('image',Q),('buffer',Q)])
Import=struct('Import',[('s',U),('n',P),('type',U),('handle',P),('name',P)])
Allocate=struct('Allocate',[('s',U),('n',P),('size',Q),('type',U)])
Pool=struct('Pool',[('s',U),('n',P),('flags',U),('family',U)])
CommandAllocate=struct('CommandAllocate',[('s',U),('n',P),('pool',Q),('level',U),('count',U)])
Begin=struct('Begin',[('s',U),('n',P),('flags',U),('inherit',P)])
Range=struct('Range',[('aspect',U),('mip',U),('mips',U),('layer',U),('layers',U)])
Layers=struct('Layers',[('aspect',U),('mip',U),('layer',U),('count',U)])
Offset=struct('Offset',[('x',I),('y',I),('z',I)])
Blit=struct('Blit',[('src',Layers),('srcOffsets',Offset*2),('dst',Layers),('dstOffsets',Offset*2)])
Barrier=struct('Barrier',[('s',U),('n',P),('src',U),('dst',U),('old',U),('new',U),('sf',U),('df',U),('image',Q),('range',Range)])
Mutex=struct('Mutex',[('s',U),('n',P),('ac',U),('sync',P),('keys',P),('timeouts',P),('rc',U),('rsync',P),('rkeys',P)])
Submit=struct('Submit',[('s',U),('n',P),('wc',U),('waits',P),('stages',P),('cc',U),('cmd',P),('sc',U),('signals',P)])
Fence=struct('Fence',[('s',U),('n',P),('flags',U)])
ID=struct('ID',[('s',U),('n',P),('uuid',c.c_ubyte*16),('driver',c.c_ubyte*16),('luid',c.c_ubyte*8),('nodes',U),('valid',U)])
Properties2=struct('Properties2',[('s',U),('n',P),('properties',c.c_ubyte*1024)])
DXDesc=struct('DXDesc',[(name,U) for name in ['w','h','mips','array','format','samples','quality','usage','bind','cpu','misc']])
Mapped=struct('Mapped',[('data',P),('row',U),('depth',U)])
root=Path(__file__).resolve().parents[2]
vk=c.WinDLL(r'C:\Windows\System32\vulkan-1.dll');native=c.CDLL(str(root/'build/CastleCraft.GpuBridge.dll'))
def function(name,args,result=I):
 f=getattr(vk,name);f.argtypes=args;f.restype=result;return f
def ok(result,label):
 if result!=0:raise RuntimeError(label+' failed: '+str(result))
def ptr(value):return c.cast(c.pointer(value),P)
def com(obj,index,result,args):
 table=c.cast(obj,c.POINTER(c.POINTER(P))).contents
 return c.WINFUNCTYPE(result,P,*args)(table[index])

if '--receiver' in sys.argv:
 dx=P();context=P();texture=P();staging=P();started=False;heartbeatStop=threading.Event()
 try:
  d3d=c.WinDLL('d3d11.dll').D3D11CreateDevice;d3d.argtypes=[P,U,P,U,P,U,U,c.POINTER(P),P,c.POINTER(P)];d3d.restype=I
  ok(d3d(None,1,None,0,None,0,7,c.byref(dx),None,c.byref(context)),'receiver D3D11')
  desc=DXDesc(64,64,1,1,28,1,0,0,40,0,0);createTexture=com(dx,5,I,[P,P,c.POINTER(P)]);ok(createTexture(dx,ptr(desc),None,c.byref(texture)),'receiver texture')
  native.CC_HostInit.argtypes=[P];assert native.CC_HostInit(texture)==1;started=True
  native.CC_HostBind.argtypes=[P];assert native.CC_HostBind(texture)==1
  native.CC_HostInfo.argtypes=[c.POINTER(I),c.POINTER(I)]
  def heartbeat():
   while not heartbeatStop.wait(.05):
    w=I();h=I();native.CC_HostInfo(c.byref(w),c.byref(h))
  worker=threading.Thread(target=heartbeat,daemon=True);worker.start()
  print('RECEIVER_READY',flush=True);assert sys.stdin.readline().strip()=='COPY'
  native.CC_RenderEvent.restype=P;c.WINFUNCTYPE(None,I)(native.CC_RenderEvent())(1);assert native.CC_HostReady()==1
  desc.usage=3;desc.bind=0;desc.cpu=0x20000;ok(createTexture(dx,ptr(desc),None,c.byref(staging)),'receiver staging')
  com(context,47,None,[P,P])(context,staging,texture);mapped=Mapped();ok(com(context,14,I,[P,U,U,U,P])(context,staging,0,1,0,ptr(mapped)),'receiver map')
  pixel=c.string_at(mapped.data,4);com(context,15,None,[P,U])(context,staging,0)
  print(pixel.hex(),flush=True);sys.stdin.readline()
 finally:
  heartbeatStop.set()
  if started:native.CC_HostClose()
  for obj in (staging,texture,context,dx):
   if obj.value:com(obj,2,U,[])(obj)
 sys.exit(0)
receiver=None
instance=P();device=P();physical=P();queue=P();dx=P();context=P();texture=P();staging=P();image=Q();memory=Q();sourceImage=Q();sourceMemory=Q();pool=Q();fence=Q();command=P();initialized=False
try:
 app=App(0,None,b'CastleCraft GPU interop probe',1,b'none',1,(1<<22)|(4<<12));info=Instance(1,None,0,c.pointer(app),0,None,0,None)
 ok(function('vkCreateInstance',[P,P,c.POINTER(P)])(ptr(info),None,c.byref(instance)),'instance')
 count=U();enumPhysical=function('vkEnumeratePhysicalDevices',[P,c.POINTER(U),P]);ok(enumPhysical(instance,c.byref(count),None),'physical count');devices=(P*count.value)();ok(enumPhysical(instance,c.byref(count),devices),'physical devices')
 physical=P(devices[0]);identity=ID(1000071004,None);props=Properties2(1000059001,ptr(identity));function('vkGetPhysicalDeviceProperties2',[P,P],None)(physical,ptr(props));assert identity.valid
 luid=bytes(identity.luid);low=int.from_bytes(luid[:4],'little');high=int.from_bytes(luid[4:],'little',signed=True)
 d3d=c.WinDLL('d3d11.dll').D3D11CreateDevice;d3d.argtypes=[P,U,P,U,P,U,U,c.POINTER(P),P,c.POINTER(P)];d3d.restype=I
 ok(d3d(None,1,None,0,None,0,7,c.byref(dx),None,c.byref(context)),'D3D11 device')
 desc=DXDesc(64,64,1,1,28,1,0,0,40,0,0);createTexture=com(dx,5,I,[P,P,c.POINTER(P)]);ok(createTexture(dx,ptr(desc),None,c.byref(texture)),'consumer texture')
 receiver=subprocess.Popen([sys.executable,str(Path(__file__).resolve()),'--receiver'],stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True,creationflags=subprocess.CREATE_NO_WINDOW)
 assert receiver.stdout.readline().strip()=='RECEIVER_READY', 'Receiver failed to initialize'
 native.CC_VulkanPrepare.argtypes=[I,I,I,I];assert native.CC_VulkanPrepare(64,64,c.c_int32(low).value,high)==1, ("native",native.CC_Error(),"hr",hex(native.CC_HResult()&0xffffffff),"luid",luid.hex())
 native.CC_VulkanHandle.restype=Q;native.CC_VulkanGeneration.restype=Q;shared=native.CC_VulkanHandle();generation=native.CC_VulkanGeneration();assert shared
 familyCount=U();families=function('vkGetPhysicalDeviceQueueFamilyProperties',[P,c.POINTER(U),P],None);families(physical,c.byref(familyCount),None);familyProperties=(QueueProperties*familyCount.value)();families(physical,c.byref(familyCount),familyProperties);family=next(i for i,p in enumerate(familyProperties) if p.flags&1)
 priority=c.c_float(1);queueInfo=QueueInfo(2,None,0,family,1,ptr(priority));extensions=(c.c_char_p*2)(b'VK_KHR_external_memory_win32',b'VK_KHR_win32_keyed_mutex');deviceInfo=DeviceInfo(3,None,0,1,ptr(queueInfo),0,None,2,c.cast(extensions,P),None)
 ok(function('vkCreateDevice',[P,P,P,c.POINTER(P)])(physical,ptr(deviceInfo),None,c.byref(device)),'device')
 function('vkGetDeviceQueue',[P,U,U,c.POINTER(P)],None)(device,family,0,c.byref(queue))
 external=External(1000072000,None,16);imageInfo=ImageInfo(14,ptr(external),0,1,37,64,64,1,1,1,1,0,2,0,0,None,0)
 ok(function('vkCreateImage',[P,P,P,c.POINTER(Q)])(device,ptr(imageInfo),None,c.byref(image)),'external image')
 requirement=Requirement();function('vkGetImageMemoryRequirements',[P,Q,P],None)(device,image,ptr(requirement))
 getProc=function('vkGetDeviceProcAddr',[P,c.c_char_p],P);getProperties=c.WINFUNCTYPE(I,P,U,P,P)(getProc(device,b'vkGetMemoryWin32HandlePropertiesKHR'))
 handleProps=HandleProperties(1000073002,None,0);ok(getProperties(device,16,P(shared),ptr(handleProps)),'handle properties');bits=handleProps.bits&requirement.bits;assert bits
 dedicated=Dedicated(1000127001,None,image.value,0);imported=Import(1000073000,ptr(dedicated),16,P(shared),None);allocate=Allocate(5,ptr(imported),requirement.size,(bits&-bits).bit_length()-1)
 ok(function('vkAllocateMemory',[P,P,P,c.POINTER(Q)])(device,ptr(allocate),None,c.byref(memory)),'memory import');ok(function('vkBindImageMemory',[P,Q,Q,Q])(device,image,memory,0),'bind')
 sourceInfo=ImageInfo(14,None,0,1,37,64,64,1,1,1,1,0,3,0,0,None,0)
 ok(function('vkCreateImage',[P,P,P,c.POINTER(Q)])(device,ptr(sourceInfo),None,c.byref(sourceImage)),'source image')
 sourceRequirements=Requirement();function('vkGetImageMemoryRequirements',[P,Q,P],None)(device,sourceImage,ptr(sourceRequirements));sourceBits=sourceRequirements.bits
 sourceAllocate=Allocate(5,None,sourceRequirements.size,(sourceBits&-sourceBits).bit_length()-1)
 ok(function('vkAllocateMemory',[P,P,P,c.POINTER(Q)])(device,ptr(sourceAllocate),None,c.byref(sourceMemory)),'source allocation');ok(function('vkBindImageMemory',[P,Q,Q,Q])(device,sourceImage,sourceMemory,0),'source bind')
 poolInfo=Pool(39,None,2,family);ok(function('vkCreateCommandPool',[P,P,P,c.POINTER(Q)])(device,ptr(poolInfo),None,c.byref(pool)),'pool')
 commandInfo=CommandAllocate(40,None,pool.value,0,1);ok(function('vkAllocateCommandBuffers',[P,P,c.POINTER(P)])(device,ptr(commandInfo),c.byref(command)),'command')
 begin=Begin(42,None,1,None);ok(function('vkBeginCommandBuffer',[P,P])(command,ptr(begin)),'begin')
 subresource=Range(1,0,1,0,1);barrier=Barrier(45,None,0,0x1000,0,1,0xfffffffe,family,image.value,subresource)
 pipeline=function('vkCmdPipelineBarrier',[P,U,U,U,U,P,U,P,U,P],None);pipeline(command,0x10000,0x1000,0,0,None,0,None,1,ptr(barrier))
 sourceBarrier=Barrier(45,None,0,0x1000,0,1,0xffffffff,0xffffffff,sourceImage.value,subresource);pipeline(command,0x10000,0x1000,0,0,None,0,None,1,ptr(sourceBarrier))
 color=(c.c_float*4)(1,0,1,1);function('vkCmdClearColorImage',[P,Q,U,P,U,P],None)(command,sourceImage,1,color,1,ptr(subresource))
 sourceBarrier.src=0x1000;sourceBarrier.dst=0x800;sourceBarrier.old=1;pipeline(command,0x1000,0x1000,0,0,None,0,None,1,ptr(sourceBarrier))
 layers=Layers(1,0,0,1);offsets=(Offset*2)(Offset(0,0,0),Offset(64,64,1));region=Blit(layers,offsets,layers,offsets)
 function('vkCmdBlitImage',[P,Q,U,Q,U,U,P,U],None)(command,sourceImage,1,image,1,1,ptr(region),0)
 barrier.src=0x1000;barrier.dst=0;barrier.old=1;barrier.sf=family;barrier.df=0xfffffffe;pipeline(command,0x1000,0x2000,0,0,None,0,None,1,ptr(barrier));ok(function('vkEndCommandBuffer',[P])(command),'end')
 sync=Q(memory.value);key=Q(0);releaseKey=Q(1);timeout=U(0);mutex=Mutex(1000075000,None,1,ptr(sync),ptr(key),ptr(timeout),1,ptr(sync),ptr(releaseKey));cmdArray=(P*1)(command.value);submit=Submit(4,ptr(mutex),0,None,None,1,c.cast(cmdArray,P),0,None);fenceInfo=Fence(8,None,0)
 ok(function('vkCreateFence',[P,P,P,c.POINTER(Q)])(device,ptr(fenceInfo),None,c.byref(fence)),'fence');ok(function('vkQueueSubmit',[P,U,P,Q])(queue,1,ptr(submit),fence),'submit');ok(function('vkWaitForFences',[P,U,P,U,Q])(device,1,ptr(fence),1,2_000_000_000),'fence wait')
 native.CC_VulkanPublish.argtypes=[Q];native.CC_VulkanPublish(generation)
 receiver.stdin.write('COPY\n');receiver.stdin.flush();pixels=bytes.fromhex(receiver.stdout.readline().strip());assert pixels==bytes([255,0,255,255]),pixels.hex()
 assert native.CC_HostReady()==1

 evidence={'gpu_pipeline_verified':True,'cross_process_verified':True,'vulkan_image_blit_verified':True,'vulkan_import_keyed_mutex_and_fence':True,'native_d3d11_consumer_verified':True,'rgba_pixel':pixels.hex(),'game_launched':False,'minecraft_mixin_runtime_verified':False,'native_sha256':hashlib.sha256((root/'build/CastleCraft.GpuBridge.dll').read_bytes()).hexdigest(),'probe_source_sha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest()}
 (root/'build/vulkan-gpu-probe.json').write_text(json.dumps(evidence,indent=2),encoding='utf-8');print('VULKAN -> D3D11 REAL GPU PROBE PASS:',pixels.hex())
finally:
 if receiver is not None:
  if receiver.poll() is None:
   receiver.stdin.write('QUIT\n');receiver.stdin.flush()
  receiver.wait(timeout=10)
 if device.value:
  function('vkDeviceWaitIdle',[P])(device)
  for name,value in [('vkDestroyFence',fence),('vkDestroyCommandPool',pool),('vkDestroyImage',sourceImage),('vkDestroyImage',image)]:
   if value.value:function(name,[P,Q,P],None)(device,value,None)
  if memory.value:function('vkFreeMemory',[P,Q,P],None)(device,memory,None)
  if sourceMemory.value:function('vkFreeMemory',[P,Q,P],None)(device,sourceMemory,None)
  function('vkDestroyDevice',[P,P],None)(device,None)
 if initialized:native.CC_HostClose()
 for obj in (staging,texture,context,dx):
  if obj.value:com(obj,2,U,[])(obj)
 if instance.value:function('vkDestroyInstance',[P,P],None)(instance,None)
