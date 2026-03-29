(function () {
    "use strict";

    var root = document.querySelector("[data-weather-root]");
    if (!root) {
        return;
    }

    var loadingState = root.querySelector("[data-loading-state]");
    var errorState = root.querySelector("[data-error-state]");
    var contentState = root.querySelector("[data-content-state]");
    var errorMessage = root.querySelector("[data-error-message]");
    var retryButton = root.querySelector("[data-retry-button]");
    var useLocationButton = root.querySelector("[data-use-location-button]");
    var locationSpinner = root.querySelector("[data-location-spinner]");
    var cityTitle = root.querySelector("[data-city-title]");
    var lastCoordinates = null;
    var locationUpdateInProgress = false;

    function setState(state, message) {
        loadingState.classList.toggle("hidden", state !== "loading");
        errorState.classList.toggle("hidden", state !== "error");
        contentState.classList.toggle("hidden", state !== "content");

        if (state === "error" && message) {
            errorMessage.textContent = message;
        }
    }

    function renderCurrent(current) {
        root.querySelector("[data-current-temp]").textContent = current.TempC.toFixed(1) + "°C";
        root.querySelector("[data-current-condition]").textContent = current.Condition;
        root.querySelector("[data-current-feels]").textContent = current.FeelsLikeC.toFixed(1) + "°C";
        root.querySelector("[data-current-humidity]").textContent = current.Humidity + "%";
        root.querySelector("[data-current-wind]").textContent = current.WindKph.toFixed(1) + " км/ч";
        root.querySelector("[data-current-updated]").textContent = current.LastUpdated;

        var icon = root.querySelector("[data-current-icon]");
        icon.src = current.IconUrl || "";
        icon.style.visibility = current.IconUrl ? "visible" : "hidden";
    }

    function updateCityTitle(city) {
        if (!cityTitle) {
            return;
        }

        cityTitle.textContent = city ? ("Погода: " + city) : "Погода: Москва";
    }

    function setLocationLoading(isLoading) {
        locationUpdateInProgress = isLoading;

        if (useLocationButton) {
            useLocationButton.disabled = isLoading;
            useLocationButton.textContent = isLoading
                ? "Определяем местоположение..."
                : "Использовать мое местоположение";
        }

        if (locationSpinner) {
            locationSpinner.classList.toggle("hidden", !isLoading);
        }
    }

    function renderHourly(hourly) {
        var hourlyList = root.querySelector("[data-hourly-list]");
        hourlyList.innerHTML = "";

        hourly.forEach(function (item) {
            var card = document.createElement("article");
            card.className = "hour-card";
            card.innerHTML =
                "<p class='muted'>" + item.Date + "</p>" +
                "<p class='hour-time'>" + item.Time + "</p>" +
                "<img class='weather-icon small' alt='Погода по часам' src='" + (item.IconUrl || "") + "' />" +
                "<p>" + item.TempC.toFixed(1) + "°C</p>" +
                "<p class='muted'>" + item.Condition + "</p>" +
                "<p class='muted'>Осадки: " + item.ChanceOfRain + "%</p>";
            hourlyList.appendChild(card);
        });
    }

    function renderDaily(daily) {
        var dailyList = root.querySelector("[data-daily-list]");
        dailyList.innerHTML = "";

        daily.forEach(function (item) {
            var card = document.createElement("article");
            card.className = "daily-card";
            card.innerHTML =
                "<div>" +
                "<p class='day-title'>" + item.Date + "</p>" +
                "<p class='muted'>" + item.Condition + "</p>" +
                "</div>" +
                "<div class='daily-main'>" +
                "<img class='weather-icon small' alt='Погода на день' src='" + (item.IconUrl || "") + "' />" +
                "<p>" + item.MinTempC.toFixed(1) + "°C / " + item.MaxTempC.toFixed(1) + "°C</p>" +
                "</div>" +
                "<div class='daily-meta muted'>" +
                "<p>Ветер до " + item.MaxWindKph.toFixed(1) + " км/ч</p>" +
                "<p>Осадки " + item.TotalPrecipMm.toFixed(1) + " мм</p>" +
                "</div>";
            dailyList.appendChild(card);
        });
    }

    function parseJsonSafe(text) {
        try {
            return JSON.parse(text);
        } catch (e) {
            return null;
        }
    }

    function loadWeather() {
        setState("loading");

        var query = "";
        if (lastCoordinates) {
            query = "?lat=" + encodeURIComponent(lastCoordinates.lat) + "&lon=" + encodeURIComponent(lastCoordinates.lon);
        }

        var xhr = new XMLHttpRequest();
        xhr.open("GET", "/Weather/Data" + query, true);
        xhr.setRequestHeader("Accept", "application/json");

        xhr.onreadystatechange = function () {
            if (xhr.readyState !== 4) {
                return;
            }

            var payload = parseJsonSafe(xhr.responseText || "");

            if (xhr.status >= 200 && xhr.status < 300 && payload) {
                try {
                    renderCurrent(payload.Current);
                    renderHourly(payload.Hourly || []);
                    renderDaily(payload.Daily || []);
                    updateCityTitle(payload.City);
                    setState("content");
                    if (locationUpdateInProgress) {
                        setLocationLoading(false);
                    }
                    return;
                } catch (renderError) {
                    setState("error", "Ошибка отображения данных: " + (renderError.message || "неизвестная ошибка"));
                    if (locationUpdateInProgress) {
                        setLocationLoading(false);
                    }
                    return;
                }
            }

            var backendMessage = payload && payload.message ? payload.message : "";
            var httpMessage = "Код ответа: " + xhr.status;
            setState("error", backendMessage || ("Не удалось загрузить данные. " + httpMessage));
            if (locationUpdateInProgress) {
                setLocationLoading(false);
            }
        };

        xhr.onerror = function () {
            setState("error", "Сетевая ошибка при запросе погоды.");
            if (locationUpdateInProgress) {
                setLocationLoading(false);
            }
        };

        xhr.send();
    }

    function useCurrentLocation() {
        setLocationLoading(true);

        if (!navigator.geolocation) {
            lastCoordinates = null;
            loadWeather();
            return;
        }

        navigator.geolocation.getCurrentPosition(
            function (position) {
                lastCoordinates = {
                    lat: position.coords.latitude,
                    lon: position.coords.longitude
                };
                loadWeather();
            },
            function () {
                lastCoordinates = null;
                loadWeather();
            },
            {
                enableHighAccuracy: false,
                timeout: 10000,
                maximumAge: 300000
            });
    }

    retryButton.addEventListener("click", loadWeather);
    if (useLocationButton) {
        useLocationButton.addEventListener("click", useCurrentLocation);
    }
    loadWeather();
})();
