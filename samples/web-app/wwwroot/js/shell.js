export function getItem(key) {
  return window.localStorage.getItem(key);
}

export function setItem(key, value) {
  window.localStorage.setItem(key, value);
}

export function keys() {
  return JSON.stringify(Object.keys(window.localStorage));
}

export function download(name, base64) {
  const binary = atob(base64);
  const bytes = new Uint8Array(binary.length);

  for (let index = 0; index < binary.length; index++) {
    bytes[index] = binary.charCodeAt(index);
  }

  const url = URL.createObjectURL(new Blob([bytes], { type: 'application/octet-stream' }));
  const link = document.createElement('a');

  link.href = url;
  link.download = name;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}
