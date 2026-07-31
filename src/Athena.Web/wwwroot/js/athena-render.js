window.athenaRenderMath = function (element) {
  if (!element) {
    return;
  }

  var run = function () {
    if (typeof renderMathInElement !== "function") {
      return false;
    }

    try {
      renderMathInElement(element, {
        delimiters: [
          { left: "$$", right: "$$", display: true },
          { left: "\\[", right: "\\]", display: true },
          { left: "$", right: "$", display: false },
          { left: "\\(", right: "\\)", display: false }
        ],
        throwOnError: false,
        ignoredTags: ["script", "noscript", "style", "textarea", "pre", "code"]
      });
    } catch (e) {
      console.warn("athenaRenderMath failed", e);
    }

    return true;
  };

  if (run()) {
    return;
  }

  // KaTeX scripts are deferred; retry briefly until auto-render is available.
  var attempts = 0;
  var timer = setInterval(function () {
    attempts += 1;
    if (run() || attempts > 40) {
      clearInterval(timer);
    }
  }, 50);
};
