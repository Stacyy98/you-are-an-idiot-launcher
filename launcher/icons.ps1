# icons.ps1 -- rebuilds the two smiley icons from youare.swf
#
#   ..\YouAreAnIdiot.ico   dark  (black disc, white face)  ... the app
#   ..\stop.ico            light (white disc, black face)  ... the STOP shortcut
#
# It renders one frame with the player completely off-screen (YouAreAnIdiot.exe
# /capture), finds the leftmost smiley, and writes classic 32bpp DIB icons --
# not PNG-compressed ones, which GDI+ cannot read.

Add-Type -AssemblyName System.Windows.Forms, System.Drawing

$here  = Split-Path -Parent $MyInvocation.MyCommand.Path
$offiz = Split-Path -Parent $here
$exe   = Join-Path $offiz "YouAreAnIdiot.exe"
$tmp   = Join-Path $env:TEMP "idiot-icons"
New-Item -ItemType Directory -Force -Path $tmp | Out-Null

# ---------------------------------------------------------------- helpers
function ConvertTo-Inverted($bmp){
  $inv = New-Object Drawing.Bitmap $bmp.Width,$bmp.Height
  $g = [Drawing.Graphics]::FromImage($inv)
  $cm = New-Object Drawing.Imaging.ColorMatrix
  $cm.Matrix00=-1; $cm.Matrix11=-1; $cm.Matrix22=-1; $cm.Matrix33=1; $cm.Matrix44=1
  $cm.Matrix40=1; $cm.Matrix41=1; $cm.Matrix42=1
  $ia = New-Object Drawing.Imaging.ImageAttributes
  $ia.SetColorMatrix($cm)
  $g.DrawImage($bmp,(New-Object Drawing.Rectangle 0,0,$bmp.Width,$bmp.Height),0,0,$bmp.Width,$bmp.Height,[Drawing.GraphicsUnit]::Pixel,$ia)
  $g.Dispose()
  return $inv
}

# ---------------------------------------------------------------- capture
$shot = Join-Path $tmp "frame.png"
Remove-Item $shot -ErrorAction SilentlyContinue
$p = Start-Process $exe -ArgumentList "/capture `"$shot`" 1380" -PassThru
[void]$p.WaitForExit(30000)
$big = [Drawing.Bitmap]::FromFile($shot)
"captured $($big.Width)x$($big.Height)"

$sum=0; $n=0
for($y=0;$y -lt $big.Height;$y+=7){ for($x=0;$x -lt $big.Width;$x+=7){ $c=$big.GetPixel($x,$y); $sum += ($c.R+$c.G+$c.B); $n++ } }
$mean = $sum/(3.0*$n)
if($mean -lt 128){ $big = ConvertTo-Inverted $big; "frame was inverted" }


# ---------------------------------------------------------------- find the face
$sw=[int]($big.Width/4); $sh=[int]($big.Height/4)
$mx0=[int]($sw*0.05); $mx1=[int]($sw*0.95)
$my0=[int]($sh*0.05); $my1=[int]($sh*0.95)
$small = New-Object Drawing.Bitmap $big,$sw,$sh
function Is-Ink($x,$y){ $c=$small.GetPixel($x,$y); return (($c.R+$c.G+$c.B) -lt 300) }

# horizontal bands: the text line is one, the three faces are another
$rows = New-Object bool[] $sh
for($y=$my0;$y -le $my1;$y++){
  for($x=$mx0;$x -le $mx1;$x++){ if(Is-Ink $x $y){ $rows[$y]=$true; break } }
}
$bands=@(); $s=-1
for($y=$my0;$y -le $my1;$y++){
  if($rows[$y] -and $s -lt 0){ $s=$y }
  elseif(-not $rows[$y] -and $s -ge 0){ if($y-$s -ge 4){ $bands += ,@($s,($y-1)) }; $s=-1 }
}
if($s -ge 0){ $bands += ,@($s,$my1) }

$found=$null
for($bi=$bands.Count-1; $bi -ge 0 -and $null -eq $found; $bi--){
  $b0=$bands[$bi][0]; $b1=$bands[$bi][1]
  $cols = New-Object bool[] $sw
  for($x=$mx0;$x -le $mx1;$x++){ for($y=$b0;$y -le $b1;$y++){ if(Is-Ink $x $y){ $cols[$x]=$true; break } } }
  $runs=@(); $s=-1
  for($x=$mx0;$x -le $mx1;$x++){
    if($cols[$x] -and $s -lt 0){ $s=$x }
    elseif(-not $cols[$x] -and $s -ge 0){ if($x-$s -ge 6){ $runs += ,@($s,($x-1)) }; $s=-1 }
  }
  if($s -ge 0){ $runs += ,@($s,$mx1) }
  foreach($run in $runs){
    $minY=99999;$maxY=-1
    for($x=$run[0];$x -le $run[1];$x++){
      for($y=$b0;$y -le $b1;$y++){ if(Is-Ink $x $y){ if($y -lt $minY){$minY=$y}; if($y -gt $maxY){$maxY=$y} } }
    }
    $rw=$run[1]-$run[0]+1; $rh=$maxY-$minY+1
    if(([Math]::Abs($rw-$rh)/[Math]::Max($rw,$rh)) -lt 0.20 -and $rw -gt 18){
      $found=@{ X0=$run[0]*4; X1=($run[1]+1)*4; Y0=$minY*4; Y1=($maxY+1)*4 }
      break
    }
  }
}
$small.Dispose()
if($null -eq $found){ throw "no smiley found in the captured frame" }
"face at $($found.X0),$($found.Y0) size $($found.X1-$found.X0)x$($found.Y1-$found.Y0)"

# square crop with a little air
$side = [int]([Math]::Max($found.X1-$found.X0,$found.Y1-$found.Y0)*1.12)
$cx=[int](($found.X0+$found.X1)/2); $cy=[int](($found.Y0+$found.Y1)/2)
$light = New-Object Drawing.Bitmap $side,$side
$fg = [Drawing.Graphics]::FromImage($light)
$fg.DrawImage($big,(New-Object Drawing.Rectangle 0,0,$side,$side),
  (New-Object Drawing.Rectangle ($cx-[int]($side/2)),($cy-[int]($side/2)),$side,$side),[Drawing.GraphicsUnit]::Pixel)
$fg.Dispose()
$big.Dispose()

$dark = ConvertTo-Inverted $light

# ---------------------------------------------------------------- icon writing
function New-IconBitmap($src,$size){
  $bmp = New-Object Drawing.Bitmap $size,$size
  $g = [Drawing.Graphics]::FromImage($bmp)
  $g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $g.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::HighQuality
  $g.DrawImage($src,(New-Object Drawing.Rectangle 0,0,$size,$size))
  $g.Dispose()
  # clip to a disc -- the crop still has paper in the corners
  $c=$size/2.0; $rad=$size/2.0-0.5
  for($y=0;$y -lt $size;$y++){
    for($x=0;$x -lt $size;$x++){
      $d=[Math]::Sqrt([Math]::Pow(($x+0.5)-$c,2)+[Math]::Pow(($y+0.5)-$c,2))
      if($d -gt $rad){
        $px=$bmp.GetPixel($x,$y); $a=0
        if($d -lt $rad+1){ $a=[int](255*(($rad+1)-$d)) }
        $bmp.SetPixel($x,$y,[Drawing.Color]::FromArgb($a,$px.R,$px.G,$px.B))
      }
    }
  }
  return $bmp
}

# one 32bpp DIB frame: BITMAPINFOHEADER + BGRA xor + 1bpp AND mask
function Get-IconFrame($bmp){
  $w=$bmp.Width; $h=$bmp.Height
  $xor = New-Object byte[] ($w*$h*4)
  $maskRow = [int]([Math]::Floor(($w+31)/32))*4
  $mask = New-Object byte[] ($maskRow*$h)
  $i=0
  for($y=$h-1;$y -ge 0;$y--){
    for($x=0;$x -lt $w;$x++){
      $p=$bmp.GetPixel($x,$y)
      $xor[$i]=$p.B; $xor[$i+1]=$p.G; $xor[$i+2]=$p.R; $xor[$i+3]=$p.A
      $i+=4
    }
  }
  $ms=New-Object IO.MemoryStream
  $bw=New-Object IO.BinaryWriter($ms)
  $bw.Write([UInt32]40); $bw.Write([Int32]$w); $bw.Write([Int32]($h*2))
  $bw.Write([UInt16]1); $bw.Write([UInt16]32)
  $bw.Write([UInt32]0); $bw.Write([UInt32]($xor.Length+$mask.Length))
  $bw.Write([Int32]0); $bw.Write([Int32]0); $bw.Write([UInt32]0); $bw.Write([UInt32]0)
  $bw.Write($xor); $bw.Write($mask); $bw.Flush()
  return ,$ms.ToArray()
}

function Write-Ico($src,$path){
  $sizes=@(256,128,64,48,32,24,16)
  $frames=@()
  foreach($s in $sizes){
    $b = New-IconBitmap $src $s
    $frames += ,@{ Size=$s; Bytes=[byte[]](Get-IconFrame $b) }
    $b.Dispose()
  }
  $fs=[IO.File]::Create($path); $bw=New-Object IO.BinaryWriter($fs)
  $bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$frames.Count)
  $off=6+16*$frames.Count
  foreach($f in $frames){
    $dim = if($f.Size -ge 256){[byte]0}else{[byte]$f.Size}
    $bw.Write($dim); $bw.Write($dim); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([UInt16]1); $bw.Write([UInt16]32)
    $bw.Write([UInt32]$f.Bytes.Length); $bw.Write([UInt32]$off)
    $off += $f.Bytes.Length
  }
  foreach($f in $frames){ $bw.Write($f.Bytes) }
  $bw.Flush(); $bw.Close(); $fs.Close()
  "wrote $(Split-Path -Leaf $path)  $([Math]::Round((Get-Item $path).Length/1kb,1)) KB  $($frames.Count) sizes"
}

Write-Ico $dark  (Join-Path $offiz "YouAreAnIdiot.ico")
Write-Ico $light (Join-Path $offiz "stop.ico")

$light.Dispose(); $dark.Dispose()
"done"
