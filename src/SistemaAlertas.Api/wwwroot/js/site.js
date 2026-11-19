"use strict";
document
  .querySelectorAll("[data-volver]")
  .forEach((b) => b.addEventListener("click", () => history.back()));
document.querySelectorAll("form[data-confirmar]").forEach((f) =>
  f.addEventListener("submit", (e) => {
    if (f.dataset.confirmar && !confirm(f.dataset.confirmar))
      e.preventDefault();
  }),
);
document.querySelectorAll("[data-geojson]").forEach((el) => {
  if (!window.L) {
    el.textContent =
      "No se pudo cargar el mapa. Consulta las instrucciones del aviso.";
    return;
  }
  try {
    const map = L.map(el).setView([-17.78, -63.25], 10);
    L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
      attribution: "© OpenStreetMap contributors",
      maxZoom: 19,
    }).addTo(map);
    const layer = L.geoJSON(JSON.parse(el.dataset.geojson), {
      style: { color: "#17684d", weight: 2, fillOpacity: 0.18 },
    }).addTo(map);
    if (layer.getBounds().isValid())
      map.fitBounds(layer.getBounds(), { maxZoom: 14 });
  } catch {
    el.textContent =
      "No se pudo representar el área. Consulta la descripción y las instrucciones.";
  }
});
