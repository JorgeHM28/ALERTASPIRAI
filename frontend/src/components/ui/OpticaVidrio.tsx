// El gris medio no desplaza el fondo. Rojo y verde codifican la curvatura
// horizontal y vertical del borde, con un centro plano para conservar la lectura.
const mapaDeBorde = `<svg xmlns="http://www.w3.org/2000/svg" width="240" height="64" viewBox="0 0 240 64" preserveAspectRatio="none">
  <defs>
    <linearGradient id="x">
      <stop stop-color="#fa8000"/><stop offset=".06" stop-color="#808000"/>
      <stop offset=".94" stop-color="#808000"/><stop offset="1" stop-color="#068000"/>
    </linearGradient>
    <linearGradient id="y" x2="0" y2="1">
      <stop stop-color="#80fa00"/><stop offset=".15" stop-color="#808000" stop-opacity="0"/>
      <stop offset=".85" stop-color="#808000" stop-opacity="0"/><stop offset="1" stop-color="#800600"/>
    </linearGradient>
  </defs>
  <rect width="240" height="64" fill="url(#x)"/><rect width="240" height="64" fill="url(#y)"/>
</svg>`

const mapa = 'data:image/svg+xml,' + encodeURIComponent(mapaDeBorde)

export function OpticaVidrio() {
  return <svg className="optica-vidrio" width="0" height="0" aria-hidden="true" focusable="false">
    <defs>
      <filter id="vidrio-bisel" x="0" y="0" width="100%" height="100%" colorInterpolationFilters="sRGB">
        <feImage href={mapa} width="100%" height="100%" preserveAspectRatio="none" result="borde" />
        <feDisplacementMap in="SourceGraphic" in2="borde" scale="18" xChannelSelector="R" yChannelSelector="G" />
      </filter>
    </defs>
  </svg>
}
