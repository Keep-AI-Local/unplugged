// Keep AI Local: tiny, dependency-free, no tracking. Picks the right download for this visitor.
(function () {
  // ---- edit these ----
  const REPO = "Keep-AI-Local/unplugged";            // GitHub owner/repository
  const DOWNLOADS_LIVE = true;                      // set to true once the first GitHub release exists
  const ASSETS = {                                  // file names attached to every GitHub release
    windows: { file: "Unplugged-Setup-Windows-x64.exe",     label: "Download for Windows" },
    mac:     { file: "Unplugged-macOS-AppleSilicon.dmg",    label: "Download for Mac" },
    linux:   { file: "Unplugged-Linux-x64.AppImage",        label: "Download for Linux" },
  };
  // ---------------------

  const gh = "https://github.com/" + REPO;
  const latest = gh + "/releases/latest";
  const fileUrl = (f) => latest + "/download/" + f;

  for (const id of ["gh-source", "gh-footer"]) { const a = document.getElementById(id); if (a) a.href = gh; }
  for (const id of ["gh-releases", "gh-latest"]) { const a = document.getElementById(id); if (a) a.href = latest; }

  const ua = navigator.userAgent || "";
  const isMobile = /Android|iPhone|iPad|iPod|Mobile/i.test(ua) && !/Macintosh/.test(ua) ||
                   (/Macintosh/.test(ua) && navigator.maxTouchPoints > 1);   // iPadOS reports "Macintosh"
  const os = /Windows/i.test(ua) ? "windows" : /Mac OS X|Macintosh/i.test(ua) ? "mac" : /Linux|X11|CrOS/i.test(ua) ? "linux" : null;

  const show = (id, on) => { const el = document.getElementById(id); if (el) el.hidden = !on; };

  // per-OS buttons
  document.querySelectorAll(".dl-os").forEach((a) => {
    const k = a.dataset.os;
    a.href = fileUrl(ASSETS[k].file);
    if (k === os) a.classList.add("mine");
  });

  // Windows: the moment the download starts, show how to get past Edge's / Windows' confirmations.
  const winSteps = document.getElementById("win-steps");
  const showWinSteps = () => { if (winSteps) { winSteps.hidden = false; winSteps.scrollIntoView({ behavior: "smooth", block: "center" }); } };
  document.querySelectorAll('.dl-os[data-os="windows"]').forEach((a) => a.addEventListener("click", showWinSteps));

  const primary = document.getElementById("dl-primary");
  if (primary) {
    if (os) { primary.href = fileUrl(ASSETS[os].file); primary.textContent = ASSETS[os].label; }
    else { primary.href = latest; primary.textContent = "See all downloads"; }
    if (os === "windows") primary.addEventListener("click", showWinSteps);
  }

  if (isMobile) {
    show("dl-mobile", true); show("dl-live", false); show("dl-soon", false);
    const hero = document.getElementById("hero-download");
    if (hero) hero.textContent = "Get it on your computer";
  } else {
    show("dl-mobile", false);
    show("dl-live", DOWNLOADS_LIVE);
    show("dl-soon", !DOWNLOADS_LIVE);
  }

  // phone: send the link to yourself
  const page = "https://keepailocal.org";
  const share = document.getElementById("share-btn");
  if (share) share.addEventListener("click", async () => {
    if (navigator.share) {
      try { await navigator.share({ title: "Keep AI Local", text: "Open this on your computer: AI that runs with no internet.", url: page }); } catch (_) {}
    } else { copy(); }
  });
  const copyBtn = document.getElementById("copy-btn");
  if (copyBtn) copyBtn.addEventListener("click", copy);
  async function copy() {
    try { await navigator.clipboard.writeText(page); show("copy-done", true); } catch (_) {}
  }
})();
