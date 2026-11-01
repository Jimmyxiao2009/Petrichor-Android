"""Native input regression checks. Start on the password tab with the demo
account field focused. Only synthetic strings are typed; no login is submitted
with a nonempty password and no SMS is sent.
"""
import json
import argparse
import re
import subprocess
import time
import xml.etree.ElementTree as ET
from pathlib import Path

parser=argparse.ArgumentParser();parser.add_argument('--out',type=Path,default=Path(__file__).resolve().parents[1]/"artifacts/polish");args=parser.parse_args()
out=args.out.resolve();out.mkdir(parents=True,exist_ok=True)
checks={}
def adb(*args):
    return subprocess.run([r"C:\tmp\PetrichorAndroid\sdk\platform-tools\adb.exe","-s","emulator-5554",*args],capture_output=True,check=True).stdout.decode("utf-8","replace")
def dump():
    for _ in range(8):
        status=adb("shell","uiautomator","dump","/sdcard/petrichor-polish.xml")
        if "dumped to" in status:return ET.fromstring(adb("shell","cat","/sdcard/petrichor-polish.xml"))
        time.sleep(.4)
    raise RuntimeError("UI hierarchy not ready")
def find(root,label):
    nodes=[n for n in root.iter("node") if label in (n.get("text"),n.get("content-desc"))]
    return next((n for n in nodes if n.get("clickable")=="true"),nodes[0] if nodes else None)
def tap(label):
    node=find(dump(),label)
    if node is None:raise RuntimeError("Missing control: "+label)
    a,b,c,d=map(int,re.findall(r"\d+",node.get("bounds")))
    adb("shell","input","tap",str((a+c)//2),str((b+d)//2));time.sleep(.15)
def check(name,condition):
    checks[name]=bool(condition)
    if not condition:raise AssertionError(name)

try:
    root=dump()
    check("three_login_tabs",all(find(root,text) is not None for text in ["手机验证码","账号密码","扫码登录"]))
    for index,value in enumerate(["demo@example.com","review.test01@example.com","test1234567890@163.com","demo@example.com"],1):
        adb("shell","input","keyevent","123",*(["67"]*48))
        adb("shell","input","text",value)
        root=dump()
        check("rapid_input_round_"+str(index),find(root,value) is not None)
    check("email_hides_country",find(root,"手机号国家区号") is None)
    # Empty password is rejected locally, before the HTTP request.
    adb("shell","input","keyevent","4");tap("登录")
    check("empty_password_validation",any(n.get("text")=="请输入密码" and n.get("class")=="android.widget.TextView" for n in dump().iter("node")))
    tap("请输入密码");adb("shell","input","text","Mock-Password-01");adb("shell","input","keyevent","4")
    root=dump()
    check("password_masked",any(n.get("password")=="true" for n in root.iter("node")) and find(root,"Mock-Password-01") is None)
    tap("显示密码");check("show_password",find(dump(),"Mock-Password-01") is not None)
    tap("隐藏密码");check("hide_password",find(dump(),"Mock-Password-01") is None)
    tap("返回");tap("登录网易云音乐");tap("账号密码")
    root=dump();check("clear_password_on_exit",find(root,"请输入密码") is not None and find(root,"显示密码") is not None)
    label=find(root,"手机号 / 网易邮箱");bounds=list(map(int,re.findall(r"\d+",label.get("bounds"))))
    check("floating_label_has_full_measured_height",bounds[3]-bounds[1]>=32)
    tap("手机验证码");check("sms_entry_retained",find(dump(),"获取验证码") is not None)
finally:
    (out/"native-input-checks.json").write_text(json.dumps({"checks":checks,"real_credentials_submitted":False,"sms_requested":False},ensure_ascii=False,indent=2),encoding="utf-8")
print(json.dumps(checks,ensure_ascii=True))
