# ROOM 13 patcher: rebuilds Game\ROOM13_Data\Managed\Assembly-CSharp.dll from the pristine backup
# (Assembly-CSharp.dll.bak) and applies every mod:
#   1) always-on guidance marker / objective text      2) credits name (EN/AR)
#   3) fear-breath player sound, quieter               4) iron-door + slide sounds (Room13IronDoor.dll)
#   5) final-stage running slide (Room13Slide.dll)
# Needs Mono.Cecil (taken from the Unity editor install) and csc.exe (.NET Framework).
param(
  [string]$Root = "E:\شهادات\ENG. Faisal\cor\ROOM 13",
  [string]$Cecil = "E:\Unity\Editors\6000.3.25f1\Editor\Data\il2cpp\build\deploy\Mono.Cecil.dll"
)
$ErrorActionPreference = 'Stop'
Add-Type -Path $Cecil
$md   = "$Root\Game\ROOM13_Data\Managed"
$dll  = "$md\Assembly-CSharp.dll"; $bak = "$dll.bak"
$mod  = "$Root\mod"; $build = "$mod\build"; New-Item -ItemType Directory -Force $build | Out-Null
$csc  = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $bak)) { throw "missing backup $bak" }
$OC = [Mono.Cecil.Cil.OpCodes]
function NewRP { $rp = New-Object Mono.Cecil.DefaultAssemblyResolver; $rp.AddSearchDirectory($md); New-Object Mono.Cecil.ReaderParameters -Property @{AssemblyResolver=$rp} }

# ---------- Phase A: public-ized copy used only to compile the mods
$a = [Mono.Cecil.ModuleDefinition]::ReadModule($bak, (NewRP))
foreach ($t in $a.Types | ? { $_.Name -in 'PlayerController','StoryDirector','R13','GameUI','AudioManager' }) {
  foreach ($f in $t.Fields)  { $f.IsPrivate = $false; $f.IsPublic = $true }
  foreach ($me in $t.Methods){ if (-not $me.IsConstructor) { $me.IsPrivate = $false; $me.IsPublic = $true } }
}
$a.Write("$build\Assembly-CSharp.dll"); $a.Dispose()
$refs = @("/r:$build\Assembly-CSharp.dll","/r:$md\netstandard.dll","/r:$md\UnityEngine.CoreModule.dll","/r:$md\UnityEngine.PhysicsModule.dll","/r:$md\UnityEngine.AudioModule.dll","/r:$md\UnityEngine.UI.dll","/r:$md\UnityEngine.UIModule.dll")
& $csc /nologo /target:library /optimize /out:"$build\Room13Slide.dll" @refs "$mod\Slide.cs"
if ($LASTEXITCODE -ne 0) { throw "Slide.cs failed to compile" }
& $csc /nologo /target:library /optimize /out:"$build\Room13IronDoor.dll" "$mod\IronDoor.cs"
if ($LASTEXITCODE -ne 0) { throw "IronDoor.cs failed to compile" }
Copy-Item "$build\Room13Slide.dll","$build\Room13IronDoor.dll" $md -Force

# ---------- Phase B: patch the pristine assembly
$m = [Mono.Cecil.ModuleDefinition]::ReadModule($bak, (NewRP))
foreach ($t in $m.Types | ? { $_.Name -eq 'PlayerController' }) { foreach ($f in $t.Fields) { if ($f.Name -in 'cc','crouchH','standH','vel') { $f.IsPrivate=$false; $f.IsPublic=$true } }
  foreach ($me in $t.Methods) { if ($me.Name -in 'CanStand','TryToggleCrouch','set_IsCrouching','set_IsSprinting') { $me.IsPrivate=$false; $me.IsPublic=$true } } }
function T($n) { $m.Types | ? { $_.Name -eq $n } }
function M($t,$n,$pc=-1) { (T $t).Methods | ? { $_.Name -eq $n -and ($pc -lt 0 -or $_.Parameters.Count -eq $pc) } }
function R4($me,$old,$new) { foreach ($i in $me.Body.Instructions) { if ($i.OpCode.Code -eq 'Ldc_R4' -and [single]$i.Operand -eq [single]$old) { $i.Operand = [single]$new } } }
function Retarget($body,$from,$to,$skip) { foreach ($i in $body.Instructions) { if ($skip -contains $i) { continue }; if ([object]::ReferenceEquals($i.Operand,$from)) { $i.Operand = $to } } }
function InsertBeforeRT($me,$target,$newInstrs) {   # insert before $target and redirect branches that pointed at it
  $il = $me.Body.GetILProcessor()
  foreach ($n in $newInstrs) { $il.InsertBefore($target,$n) }
  Retarget $me.Body $target $newInstrs[0] $newInstrs
}

# 1) guidance marker + persistent objective text
$obj = M GameUI Objective 3; R4 $obj 6 1e9; R4 $obj 8 1e9
foreach ($i in (M GameUI BuildMarker).Body.Instructions) {
  if ($i.OpCode.Code -eq 'Ldc_R4') { $v=[single]$i.Operand; if ($v -eq 30) {$i.Operand=[single]80} elseif ($v -eq 16) {$i.Operand=[single]40} elseif ($v -eq 26) {$i.Operand=[single]76} }
  if ($i.OpCode.Code -eq 'Ldc_I4_S' -and [int]$i.Operand -eq 18) { $i.Operand=[sbyte]32 } }

# 2) credits name + hint translations (LocTable.Ar)
($(M GameUI .cctor).Body.Instructions | ? { $_.OpCode.Code -eq 'Ldstr' -and "$($_.Operand)" -eq 'Claude' }).Operand = "Eng. Faisal"
$lt = M LocTable .cctor; $ilt = $lt.Body.GetILProcessor(); $ins = $lt.Body.Instructions
$fa = $ins | ? { $_.OpCode.Code -eq 'Ldstr' -and "$($_.Operand)" -eq 'Faisal' } | select -First 1
$addCall = ($ins | ? { $_.OpCode.Code -eq 'Callvirt' -and "$($_.Operand)" -match 'Dictionary.*::Add' } | select -First 1).Operand
$anchor = $fa.Next.Next; if ($anchor.OpCode.Code -ne 'Callvirt') { throw "unexpected LocTable layout" }
foreach ($pair in @(@("Eng. Faisal","المهندس فيصل"), @("Sprint + C to SLIDE under obstacles","اركض واضغط C للانزلاق تحت العوائق"))) {
  $i1=$ilt.Create($OC::Dup); $i2=$ilt.Create($OC::Ldstr,$pair[0]); $i3=$ilt.Create($OC::Ldstr,$pair[1]); $i4=$ilt.Create($OC::Callvirt,$addCall)
  $ilt.InsertAfter($anchor,$i1); $ilt.InsertAfter($i1,$i2); $ilt.InsertAfter($i2,$i3); $ilt.InsertAfter($i3,$i4); $anchor=$i4 }

# 3) player sound: fear breaths, quieter
$aw = M AudioManager Awake; $x = $aw.Body.Instructions
for ($k=0; $k -lt $x.Count-1; $k++) { if ($x[$k].OpCode.Code -eq 'Ldstr' -and "$($x[$k].Operand)" -eq 'breath' -and "$($x[$k+1].Operand)" -match 'SynthAudio::Get') { $x[$k].Operand='breath_fast' } }
($x | ? { $_.OpCode.Code -eq 'Ldc_R4' -and [single]$_.Operand -eq [single]0.18 } | select -First 1).Operand = [single]0.07
R4 (M AudioManager Update) 0.14 0.05
$sb = M AudioManager SetBreathing; $ilp = $sb.Body.GetILProcessor(); $a1 = $sb.Body.Instructions | ? { $_.OpCode.Code -eq 'Ldarg_1' } | select -First 1
$ilp.InsertAfter($a1,$ilp.Create($OC::Ldc_R4,[single]0.4)); $ilp.InsertAfter($a1.Next,$ilp.Create($OC::Mul))
R4 (M PlayerController Update) 0.6 0.25
$fs = M PlayerController Footstep; $ilf = $fs.Body.GetILProcessor()
$vl = $fs.Body.Instructions | ? { $_.OpCode.Code -eq 'Ldloc_2' -and $_.Next -and $_.Next.OpCode.Code -eq 'Ldc_R4' -and [single]$_.Next.Operand -eq [single]0.92 } | select -First 1
$ilf.InsertAfter($vl,$ilf.Create($OC::Ldc_R4,[single]0.3)); $ilf.InsertAfter($vl.Next,$ilf.Create($OC::Mul))

# 4) iron-door / slide sounds: HorrorSynth.Generate(name) first asks Room13Mod.IronDoor.Generate(name)
$door = [Mono.Cecil.ModuleDefinition]::ReadModule("$build\Room13IronDoor.dll")
$gen  = $door.Types | ? { $_.Name -eq 'IronDoor' } | % { $_.Methods } | ? { $_.Name -eq 'Generate' }
$ref  = $m.ImportReference($gen)
$hs = M HorrorSynth Generate; $ih = $hs.Body.GetILProcessor(); $first = $hs.Body.Instructions[0]; $pop = $ih.Create($OC::Pop)
InsertBeforeRT $hs $first @($ih.Create($OC::Ldarg_0), $ih.Create($OC::Call,$ref), $ih.Create($OC::Dup), $ih.Create($OC::Brfalse,$pop), $ih.Create($OC::Ret), $pop)

# 5) final-stage slide
$sl = [Mono.Cecil.ModuleDefinition]::ReadModule("$build\Room13Slide.dll")
$S  = $sl.Types | ? { $_.Name -eq 'Slide' }
function SM($n) { $m.ImportReference(($S.Methods | ? { $_.Name -eq $n })) }
$upd = M PlayerController Update; $ui = $upd.Body.GetILProcessor(); $ui2 = $upd.Body.Instructions
# 5a) Tick() right after the "paused" early-out
$retIdx = 0..($ui2.Count-1) | ? { $ui2[$_].OpCode.Code -eq 'Ret' } | select -First 1
$tgt = $ui2[$retIdx+1]
InsertBeforeRT $upd $tgt @($ui.Create($OC::Ldarg_0), $ui.Create($OC::Call,(SM 'Tick')))
# 5b) input override after "input = Vector2.ClampMagnitude(input,1)"
$cm = $ui2 | ? { $_.OpCode.Code -eq 'Call' -and "$($_.Operand)" -match 'Vector2::ClampMagnitude' } | select -First 1
$st = $cm.Next; if ($st.OpCode.Code -ne 'Stloc_1') { throw "ClampMagnitude layout changed" }
$p1=$ui.Create($OC::Ldarg_0); $p2=$ui.Create($OC::Ldloc_1); $p3=$ui.Create($OC::Call,(SM 'ModInput')); $p4=$ui.Create($OC::Stloc_1)
$ui.InsertAfter($st,$p1); $ui.InsertAfter($p1,$p2); $ui.InsertAfter($p2,$p3); $ui.InsertAfter($p3,$p4)
# 5c) speed override: the first "stloc V_4" that follows a crouchSpeed load
$cs = $ui2 | ? { $_.OpCode.Code -eq 'Ldfld' -and "$($_.Operand)" -match 'crouchSpeed' } | select -First 1
$sv = $cs.Next; if ($sv.OpCode.Code -ne 'Stloc_S') { throw "speed local layout changed" }
$vSpeed = $sv.Operand
$q1=$ui.Create($OC::Ldarg_0); $q2=$ui.Create($OC::Ldloc_S,$vSpeed); $q3=$ui.Create($OC::Call,(SM 'ModSpeed')); $q4=$ui.Create($OC::Stloc_S,$vSpeed)
$ui.InsertAfter($sv,$q1); $ui.InsertAfter($q1,$q2); $ui.InsertAfter($q2,$q3); $ui.InsertAfter($q3,$q4)
# 5d) eye height override: the stloc after the crouchEye / eye selection
$ce = $ui2 | ? { $_.OpCode.Code -eq 'Ldfld' -and "$($_.Operand)" -match '::crouchEye' } | select -First 1
$ev = $ce.Next; if ($ev.OpCode.Code -ne 'Stloc_S') { throw "eye local layout changed" }
$vEye = $ev.Operand
$e1=$ui.Create($OC::Ldarg_0); $e2=$ui.Create($OC::Ldloc_S,$vEye); $e3=$ui.Create($OC::Call,(SM 'ModEye')); $e4=$ui.Create($OC::Stloc_S,$vEye)
$ui.InsertAfter($ev,$e1); $ui.InsertAfter($e1,$e2); $ui.InsertAfter($e2,$e3); $ui.InsertAfter($e3,$e4)
# 5e) key hook + stand-lock
$tc = M PlayerController TryToggleCrouch; $itc = $tc.Body.GetILProcessor(); $f0 = $tc.Body.Instructions[0]
InsertBeforeRT $tc $f0 @($itc.Create($OC::Ldarg_0), $itc.Create($OC::Call,(SM 'TryStart')), $itc.Create($OC::Brfalse,$f0), $itc.Create($OC::Ret))
$cst = M PlayerController CanStand; $ics = $cst.Body.GetILProcessor(); $g0 = $cst.Body.Instructions[0]
InsertBeforeRT $cst $g0 @($ics.Create($OC::Call,(SM 'BlocksStand')), $ics.Create($OC::Brfalse,$g0), $ics.Create($OC::Ldc_I4_0), $ics.Create($OC::Ret))

$m.Write("$dll.new"); $m.Dispose(); $door.Dispose(); $sl.Dispose()
Move-Item "$dll.new" $dll -Force
"patched: $((Get-Item $dll).Length) bytes"


