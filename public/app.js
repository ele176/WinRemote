(function () {
  var tokenKey = "winremote_token";
  var token = findToken();
  var screen = document.getElementById("screen");
  var streamStage = document.getElementById("streamStage");
  var emptyState = document.getElementById("emptyState");
  var connectionText = document.getElementById("connectionText");
  var pairPanel = document.getElementById("pairPanel");
  var pairCode = document.getElementById("pairCode");
  var pairBtn = document.getElementById("pairBtn");
  var pairStatus = document.getElementById("pairStatus");
  var keyboardPanel = document.getElementById("keyboardPanel");
  var keyboardBtn = document.getElementById("keyboardBtn");
  var textInput = document.getElementById("textInput");
  var leftBtn = document.getElementById("leftBtn");
  var rightBtn = document.getElementById("rightBtn");
  var doubleBtn = document.getElementById("doubleBtn");
  var reconnectBtn = document.getElementById("reconnectBtn");
  var backspaceBtn = document.getElementById("backspaceBtn");
  var enterBtn = document.getElementById("enterBtn");
  var escBtn = document.getElementById("escBtn");
  var wheelUpBtn = document.getElementById("wheelUpBtn");
  var wheelDownBtn = document.getElementById("wheelDownBtn");

  var activeButton = "left";
  var lastMoveAt = 0;
  var pointerActive = false;
  var lastPoint = null;
  var textValue = "";
  var streamNonce = 0;

  setup();

  function setup() {
    screen.addEventListener("load", function () {
      emptyState.classList.add("hidden");
    });

    screen.addEventListener("error", function () {
      emptyState.classList.remove("hidden");
      connectionText.textContent = "连接断开";
    });

    streamStage.addEventListener("pointerdown", onPointerDown);
    streamStage.addEventListener("pointermove", onPointerMove);
    streamStage.addEventListener("pointerup", onPointerUp);
    streamStage.addEventListener("pointercancel", onPointerCancel);
    streamStage.addEventListener("contextmenu", function (event) {
      event.preventDefault();
    });

    leftBtn.addEventListener("click", function () { setButton("left"); });
    rightBtn.addEventListener("click", function () { setButton("right"); });
    doubleBtn.addEventListener("click", function () {
      if (lastPoint) sendInput({ action: "doubleclick", x: lastPoint.x, y: lastPoint.y });
    });
    reconnectBtn.addEventListener("click", reconnect);
    wheelUpBtn.addEventListener("click", function () { sendInput({ action: "wheel", delta: 360 }); });
    wheelDownBtn.addEventListener("click", function () { sendInput({ action: "wheel", delta: -360 }); });
    keyboardBtn.addEventListener("click", toggleKeyboard);
    backspaceBtn.addEventListener("click", function () { sendInput({ action: "key", key: "Backspace" }); });
    enterBtn.addEventListener("click", function () { sendInput({ action: "key", key: "Enter" }); });
    escBtn.addEventListener("click", function () { sendInput({ action: "key", key: "Escape" }); });

    textInput.addEventListener("input", onTextInput);
    textInput.addEventListener("keydown", onPhysicalKey);

    pairBtn.addEventListener("click", pair);
    pairCode.addEventListener("input", function () {
      pairCode.value = pairCode.value.replace(/\D/g, "").slice(0, 8);
      pairStatus.classList.remove("error");
    });
    pairCode.addEventListener("keydown", function (event) {
      if (event.key === "Enter") pair();
    });

    if (token) {
      hidePair();
      start();
    } else {
      showPair();
    }
  }

  function findToken() {
    var params = new URLSearchParams(window.location.search);
    var urlToken = params.get("token");
    if (urlToken) {
      localStorage.setItem(tokenKey, urlToken);
      document.cookie = "wr_token=" + encodeURIComponent(urlToken) + "; Path=/; SameSite=Strict";
      window.history.replaceState({}, document.title, window.location.pathname);
      return urlToken;
    }

    return localStorage.getItem(tokenKey) || "";
  }

  function showPair() {
    pairPanel.classList.remove("hidden");
    setTimeout(function () { pairCode.focus(); }, 50);
  }

  function hidePair() {
    pairPanel.classList.add("hidden");
  }

  function pair() {
    var code = pairCode.value.trim();
    if (code.length !== 8) {
      setPairError("请输入 8 位配对码");
      return;
    }

    pairBtn.disabled = true;
    pairStatus.textContent = "配对中";

    fetch("/api/pair", {
      method: "POST",
      headers: { "Content-Type": "application/x-www-form-urlencoded" },
      body: form({ code: code })
    }).then(function (response) {
      return response.json();
    }).then(function (data) {
      if (!data.ok || !data.token) {
        throw new Error("bad_code");
      }

      token = data.token;
      localStorage.setItem(tokenKey, token);
      document.cookie = "wr_token=" + encodeURIComponent(token) + "; Path=/; SameSite=Strict";
      hidePair();
      start();
    }).catch(function () {
      setPairError("配对失败，连续错误会临时锁定");
    }).finally(function () {
      pairBtn.disabled = false;
    });
  }

  function setPairError(message) {
    pairStatus.textContent = message;
    pairStatus.classList.add("error");
  }

  function start() {
    connectionText.textContent = "连接中";
    fetchStatus();
    reconnect();
  }

  function reconnect() {
    if (!token) {
      showPair();
      return;
    }

    emptyState.classList.remove("hidden");
    connectionText.textContent = "连接中";
    streamNonce++;
    screen.src = "/stream.mjpg?fps=8&width=1280&quality=55&token=" + encodeURIComponent(token) + "&n=" + streamNonce;
  }

  function fetchStatus() {
    authedFetch("/api/status").catch(function () {
      localStorage.removeItem(tokenKey);
      token = "";
      screen.removeAttribute("src");
      showPair();
    });
  }

  function onPointerDown(event) {
    if (!token || event.button > 0) return;
    event.preventDefault();
    streamStage.setPointerCapture(event.pointerId);
    pointerActive = true;

    var point = eventToPoint(event);
    if (!point) return;

    lastPoint = point;
    if (activeButton === "right") {
      sendInput({ action: "rightclick", x: point.x, y: point.y });
      pointerActive = false;
      return;
    }

    sendInput({ action: "down", button: "left", x: point.x, y: point.y });
  }

  function onPointerMove(event) {
    if (!token || !pointerActive) return;
    event.preventDefault();

    var now = Date.now();
    if (now - lastMoveAt < 45) return;
    lastMoveAt = now;

    var point = eventToPoint(event);
    if (!point) return;

    lastPoint = point;
    sendInput({ action: "move", x: point.x, y: point.y });
  }

  function onPointerUp(event) {
    if (!token || !pointerActive) return;
    event.preventDefault();
    pointerActive = false;

    var point = eventToPoint(event) || lastPoint;
    if (!point) return;

    lastPoint = point;
    sendInput({ action: "up", button: "left", x: point.x, y: point.y });
  }

  function onPointerCancel() {
    pointerActive = false;
    if (lastPoint) {
      sendInput({ action: "up", button: "left", x: lastPoint.x, y: lastPoint.y });
    }
  }

  function eventToPoint(event) {
    var rect = screen.getBoundingClientRect();
    if (!rect.width || !rect.height) return null;

    var x = (event.clientX - rect.left) / rect.width;
    var y = (event.clientY - rect.top) / rect.height;

    if (x < 0 || x > 1 || y < 0 || y > 1) return null;
    return { x: clamp(x), y: clamp(y) };
  }

  function setButton(button) {
    activeButton = button;
    leftBtn.classList.toggle("active", button === "left");
    rightBtn.classList.toggle("active", button === "right");
  }

  function toggleKeyboard() {
    keyboardPanel.classList.toggle("open");
    if (keyboardPanel.classList.contains("open")) {
      textInput.focus();
    } else {
      textInput.blur();
    }
  }

  function onTextInput() {
    var value = textInput.value;
    var prefix = commonPrefixLength(textValue, value);
    var deleted = textValue.length - prefix;
    var added = value.slice(prefix);

    for (var i = 0; i < deleted; i++) {
      sendInput({ action: "key", key: "Backspace" });
    }
    if (added) {
      sendInput({ action: "text", text: added });
    }

    textValue = value;
    if (textValue.length > 80) {
      textValue = "";
      textInput.value = "";
    }
  }

  function onPhysicalKey(event) {
    var keys = {
      Backspace: true,
      Enter: true,
      Tab: true,
      Escape: true,
      Delete: true,
      ArrowLeft: true,
      ArrowUp: true,
      ArrowRight: true,
      ArrowDown: true
    };

    if (!keys[event.key]) return;
    event.preventDefault();
    sendInput({ action: "key", key: event.key });
  }

  function sendInput(data) {
    if (!token) return Promise.resolve();

    return authedFetch("/api/input", {
      method: "POST",
      headers: { "Content-Type": "application/x-www-form-urlencoded" },
      body: form(data)
    }).catch(function () {});
  }

  function authedFetch(url, options) {
    options = options || {};
    options.headers = options.headers || {};
    options.headers["X-Remote-Token"] = token;
    return fetch(url, options).then(function (response) {
      if (!response.ok) throw new Error(String(response.status));
      return response;
    });
  }

  function form(data) {
    var params = new URLSearchParams();
    Object.keys(data).forEach(function (key) {
      params.set(key, String(data[key]));
    });
    return params.toString();
  }

  function commonPrefixLength(a, b) {
    var max = Math.min(a.length, b.length);
    var i = 0;
    while (i < max && a.charAt(i) === b.charAt(i)) i++;
    return i;
  }

  function clamp(value) {
    if (value < 0) return 0;
    if (value > 1) return 1;
    return value;
  }
})();
