$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
Set-Location $repoRoot

$edge = Join-Path ${env:ProgramFiles(x86)} "Microsoft\Edge\Application\msedge.exe"
if (!(Test-Path -LiteralPath $edge)) {
  Write-Output "MEMBER_CARD_LOAD_MISSING_EDGE"
  exit 1
}

$scriptRoot = (Resolve-Path "app/src/js").Path.Replace("\", "/")
$base = "file:///" + $scriptRoot
$htmlPath = Join-Path $env:TEMP "pb-member-card-load-check.html"

$html = @'
<!doctype html>
<html><head><meta charset="utf-8"><title>pending</title></head>
<body>
<div id="appOutput"></div>
<div id="sessionStatus"></div>
<div id="tree"></div>
<div id="detail"></div>
<script>
const base = "BASE";
const scripts = ["api-client.js", "navigator.js", "legacy-app.js"];
function loadScript(src) {
  return new Promise(function (resolve, reject) {
    const script = document.createElement("script");
    script.src = src;
    script.async = false;
    script.onload = resolve;
    script.onerror = function () { reject(new Error("Failed to load " + src)); };
    document.head.appendChild(script);
  });
}
scripts.reduce(function (chain, src) {
  return chain.then(function () { return loadScript(base + "/" + src); });
}, Promise.resolve()).then(function () {
  const systemId = "11111111-1111-1111-1111-111111111111";
  const ids = [
    "ec5bae4b-4a9b-ae63-836c-319a50aae8b4",
    "9366ff18-03e1-90f6-2722-d466ecca440c",
    "bc511fd9-7a92-364a-1e6e-0031beb7b1c4"
  ];
  const names = ["Alex", "Morgan", "Rae"];
  const systemPath = "01|000000|system|" + systemId;
  const rows = [{
    pathKey: systemPath,
    nodeLabel: "System: The Harbor",
    objectType: "system",
    objectId: systemId,
    parentPathKey: null,
    header: false,
    anchorName: "The Harbor"
  }];
  ids.forEach(function (id, index) {
    rows.push({
      pathKey: systemPath + ".10|00000" + (index + 1) + "|member|" + id,
      nodeLabel: "Member: " + names[index],
      objectType: "member",
      objectId: id.toUpperCase(),
      parentPathKey: systemPath,
      header: false,
      anchorName: null
    });
  });
  rows.splice(2, 0, {
    pathKey: systemPath + ".12|000001|group|22222222-2222-2222-2222-222222222222",
    nodeLabel: "Group: Visitors",
    objectType: "group",
    objectId: "22222222-2222-2222-2222-222222222222",
    parentPathKey: systemPath,
    header: false,
    anchorName: "Visitors"
  });
  const profiles = ids.map(function (id, index) {
    return {
      memberId: id,
      displayName: names[index],
      pronouns: index === 2 ? null : "they/them",
      description: "Profile for " + names[index]
    };
  });
  const requested = [];
  window.fetch = function (url, options) {
    const text = String(url);
    requested.push(text);
    const body = text.indexOf("plural-map") >= 0
      ? JSON.stringify({ hierarchyPreview: rows })
      : JSON.stringify(profiles);
    return Promise.resolve({
      ok: true,
      status: 200,
      text: function () { return Promise.resolve(body); },
      json: function () { return Promise.resolve(JSON.parse(body)); }
    });
  };
  window.PluralBridge.navigator.mount({
    tree: document.getElementById("tree"),
    detail: document.getElementById("detail")
  });
  return new Promise(function (resolve) { setTimeout(resolve, 300); }).then(function () {
    const detail = document.getElementById("detail");
    const cardNames = Array.prototype.map.call(detail.querySelectorAll(".member-name"), function (node) {
      return node.textContent;
    });
    const profileUrl = requested.filter(function (url) {
      return url.indexOf("anchor-member-profiles") >= 0;
    })[0] || "";
    const editable = detail.querySelectorAll(".member-profile-field-edit-button, .member-profile-save-button").length;
    const failure = detail.querySelector(".navigator-status-error");
    const problems = [];
    if (typeof window.PluralBridge.createMemberAccordion !== "function") {
      problems.push("export missing");
    }
    if (cardNames.join(",") !== "Alex,Morgan,Rae") {
      problems.push("names " + cardNames.join(","));
    }
    if (detail.querySelectorAll(".member-card").length !== 3) {
      problems.push("card count");
    }
    if (detail.querySelectorAll(".member-map-child").length !== 0) {
      problems.push("non-member row");
    }
    if (detail.innerText.indexOf("Group: Visitors") >= 0) {
      problems.push("non-member label");
    }
    if (editable !== 0) {
      problems.push("editable controls");
    }
    if (failure) {
      problems.push("pane error");
    }
    if (profileUrl.indexOf("objectType=system") < 0 || profileUrl.indexOf("anchorName=The+Harbor") < 0) {
      problems.push("profile url " + profileUrl);
    }
    document.title = problems.length === 0
      ? "MEMBER_CARD_LOAD_OK"
      : "MEMBER_CARD_LOAD_FAIL " + problems.join("; ");
  });
}).catch(function (error) {
  document.title = "MEMBER_CARD_LOAD_FAIL " + error.name + " " + error.message;
});
</script>
</body></html>
'@

$html = $html.Replace("BASE", $base)
Set-Content -Path $htmlPath -Value $html -Encoding UTF8

$profileDir = Join-Path $env:TEMP ("pb-member-card-load-" + [guid]::NewGuid().ToString("N"))
$stdoutPath = Join-Path $env:TEMP "pb-member-card-load-out.txt"
$stderrPath = Join-Path $env:TEMP "pb-member-card-load-err.txt"
New-Item -ItemType Directory -Path $profileDir | Out-Null
try {
  $process = Start-Process -FilePath $edge -ArgumentList @(
    "--headless",
    "--disable-gpu",
    "--user-data-dir=$profileDir",
    "--virtual-time-budget=8000",
    "--dump-dom",
    $htmlPath
  ) -Wait -PassThru -NoNewWindow -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath
} finally {
  Remove-Item -LiteralPath $profileDir -Recurse -Force -ErrorAction SilentlyContinue
}

$dump = if (Test-Path $stdoutPath) { Get-Content -Path $stdoutPath -Raw } else { "" }
$title = [regex]::Match($dump, "<title>(?<title>.*?)</title>").Groups["title"].Value

if ($title -eq "MEMBER_CARD_LOAD_OK") {
  Write-Output "MEMBER_CARD_LOAD_OK"
  exit 0
}

Write-Output $title
exit 1
