/**
 * PNG → ICO conversion helpers
 * Renders any browser-decodable image onto a square canvas (contain mode)
 * and encodes as a single-size ICO binary.
 */

function renderPngToSquareCanvas(img, size) {
  const canvas = document.createElement('canvas')
  canvas.width = size
  canvas.height = size
  const ctx = canvas.getContext('2d')
  ctx.clearRect(0, 0, size, size)
  const scale = Math.min(size / img.width, size / img.height)
  const w = img.width * scale
  const h = img.height * scale
  const x = (size - w) / 2
  const y = (size - h) / 2
  ctx.drawImage(img, x, y, w, h)
  return canvas
}

function getCanvasBlob(canvas) {
  return new Promise((resolve, reject) => {
    canvas.toBlob((blob) => {
      if (blob) resolve(blob)
      else reject(new Error('canvas.toBlob returned null'))
    }, 'image/png')
  })
}

async function encodeSingleIco(img, targetSize) {
  const canvas = renderPngToSquareCanvas(img, targetSize)
  const blob = await getCanvasBlob(canvas)
  const buffer = await blob.arrayBuffer()

  const headerSize = 6
  const directorySize = 16
  const totalSize = headerSize + directorySize + buffer.byteLength

  const compiledBuffer = new ArrayBuffer(totalSize)
  const view = new DataView(compiledBuffer)
  const uint8Array = new Uint8Array(compiledBuffer)

  // ICO header
  view.setUint16(0, 0, true)        // Reserved
  view.setUint16(2, 1, true)        // Type: 1 = ICO
  view.setUint16(4, 1, true)        // Count: 1 image

  // Directory entry
  const sizeByte = targetSize >= 256 ? 0 : targetSize
  view.setUint8(6, sizeByte)        // Width
  view.setUint8(7, sizeByte)        // Height
  view.setUint8(8, 0)               // ColorCount
  view.setUint8(9, 0)               // Reserved
  view.setUint16(10, 1, true)       // Planes
  view.setUint16(12, 32, true)      // BitCount
  view.setUint32(14, buffer.byteLength, true)  // BytesInRes
  view.setUint32(18, headerSize + directorySize, true) // ImageOffset

  // PNG data
  uint8Array.set(new Uint8Array(buffer), headerSize + directorySize)

  return new Blob([compiledBuffer], { type: 'image/x-icon' })
}

/**
 * Convert a browser-decodable image file (PNG, JPEG, WebP, BMP, GIF, AVIF, etc.)
 * to a single-size ICO Blob.
 * Resolution: short side >= 300 → 512×512, otherwise → 256×256.
 * Scaling mode: contain (centered, transparent padding).
 */
export async function convertImageToIco(file) {
  const img = await loadImage(file)
  const shortSide = Math.min(img.width, img.height)
  const targetSize = shortSide >= 300 ? 512 : 256
  const icoBlob = await encodeSingleIco(img, targetSize)
  const fileName = file.name.replace(/\.[^.]+$/, '.ico')
  // Generate a PNG preview from the canvas (browsers can't reliably render
  // image/x-icon in <img> tags, so we use this for display)
  const canvas = renderPngToSquareCanvas(img, targetSize)
  const previewUrl = canvas.toDataURL('image/png')
  return { icoBlob, fileName, previewUrl }
}

function loadImage(file) {
  return new Promise((resolve, reject) => {
    const img = new Image()
    const url = URL.createObjectURL(file)
    img.onload = () => { URL.revokeObjectURL(url); resolve(img) }
    img.onerror = () => { URL.revokeObjectURL(url); reject(new Error('Image decode failed')) }
    img.src = url
  })
}
