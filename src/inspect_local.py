from pathlib import Path
import re, json, shlex
p=Path(r'D:\新版客户端\PCL\LatestLaunch.bat')
s=p.read_text(encoding='utf-8-sig')
for line in s.splitlines():
    if 'javaw.exe' in line or 'java.exe' in line:
        print('Java:', re.search(r'"([^"]*javaw?\.exe)"',line).group(1))
        for flag in ('--version','--gameDir','--assetsDir','--uuid','--accessToken','--username'):
            m=re.search(re.escape(flag)+r'\s+("[^"]*"|\S+)',line)
            val=m.group(1).strip('"') if m else ''
            print(flag, val if flag in ('--version','--gameDir','--assetsDir') else ('present, length='+str(len(val))+', zero='+str(val in ('0','null',''))))
        for m in re.finditer(r'"?-D(?:java.library.path|org.lwjgl.librarypath)=([^" ]+)',line): print('Native directory:',m.group(1))
