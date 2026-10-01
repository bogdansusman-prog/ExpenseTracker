/**
 * Crops an image file to a centered square and resizes it in the browser,
 * returning a small data URL that is cheap to store and send.
 */
export function resizeImageToDataUrl(file: File, size = 256, quality = 0.85): Promise<string> {
  return new Promise((resolve, reject) => {
    if (!file.type.startsWith('image/')) {
      reject(new Error('not-an-image'));
      return;
    }

    const url = URL.createObjectURL(file);
    const image = new Image();

    image.onload = () => {
      const side = Math.min(image.naturalWidth, image.naturalHeight);
      const sx = (image.naturalWidth - side) / 2;
      const sy = (image.naturalHeight - side) / 2;

      const canvas = document.createElement('canvas');
      canvas.width = size;
      canvas.height = size;

      const context = canvas.getContext('2d');
      if (!context) {
        URL.revokeObjectURL(url);
        reject(new Error('no-canvas'));
        return;
      }

      context.imageSmoothingQuality = 'high';
      context.drawImage(image, sx, sy, side, side, 0, 0, size, size);
      URL.revokeObjectURL(url);

      // WebP keeps transparency and is small; fall back to JPEG if the browser can't encode it.
      const webp = canvas.toDataURL('image/webp', quality);
      resolve(webp.startsWith('data:image/webp') ? webp : canvas.toDataURL('image/jpeg', quality));
    };

    image.onerror = () => {
      URL.revokeObjectURL(url);
      reject(new Error('load-failed'));
    };

    image.src = url;
  });
}
