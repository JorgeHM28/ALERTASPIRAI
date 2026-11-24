declare module 'liquid-gl' {
  export default function liquidGL(options: {
    target: string
    snapshot: string
    engine: 'webgl'
    tint: string
    resolution: number
    refraction: number
    bevelDepth: number
    bevelWidth: number
    frost: number
    aberration: number
    shadow: boolean
    specular: boolean
    tilt: boolean
    reveal: 'none'
    content: boolean
  }): { destroy(): void } | undefined
}
