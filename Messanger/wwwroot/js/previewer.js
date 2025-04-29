
(function() {
    class Handler {
        constructor() {
            this.next = null;
        }

        setNext(handler) {
            this.next = handler;
            return handler;
        }

        handle(request) {
            if (this.next) {
                return this.next.handle(request);
            }
            return null;
        }
    }

    class ImageHandler extends Handler {
        canHandle({ file, url }) {
            if (file) {
                return file.type.startsWith('image/');
            } else if (url) {
                return /\.(jpg|jpeg|png|gif|bmp|webp)(\?.*)?$/i.test(url);
            }
            return false;
        }

        handle(request) {
            if (this.canHandle(request)) {
                const img = document.createElement('img');
                img.style.maxWidth = '100%';
                img.style.maxHeight = '300px';
                img.src = request.file ? URL.createObjectURL(request.file) : request.url;
                return img;
            }
            return super.handle(request);
        }
    }

    class VideoHandler extends Handler {
        canHandle({ file, url }) {
            if (file) {
                return file.type.startsWith('video/');
            } else if (url) {
                return /\.(mp4|webm|ogg)(\?.*)?$/i.test(url);
            }
            return false;
        }

        handle(request) {
            if (this.canHandle(request)) {
                const video = document.createElement('video');
                video.controls = true;
                video.style.maxWidth = '100%';
                video.style.maxHeight = '300px';
                video.src = request.file ? URL.createObjectURL(request.file) : request.url;
                return video;
            }
            return super.handle(request);
        }
    }

    class AudioHandler extends Handler {
        canHandle({ file, url }) {
            if (file) {
                return file.type.startsWith('audio/');
            } else if (url) {
                return /\.(mp3|wav|ogg|flac)(\?.*)?$/i.test(url);
            }
            return false;
        }

        handle(request) {
            if (this.canHandle(request)) {
                const audio = document.createElement('audio');
                audio.controls = true;
                audio.src = request.file ? URL.createObjectURL(request.file) : request.url;
                return audio;
            }
            return super.handle(request);
        }
    }

    class LinkHandler extends Handler {
       
        handle(request) {
            const link = document.createElement('a');
            link.href = request.url || '#';
            link.textContent = request.url || 'Невідомий ресурс';
            link.target = '_blank';
            return link;
        }
    }

    const previewChain = new ImageHandler();
    previewChain
        .setNext(new VideoHandler())
        .setNext(new AudioHandler())
        .setNext(new LinkHandler());


    window.preview = function(input, container) {
        const request = {};
        let downloadUrl = null;
        let downloadName = '';

        if (input instanceof File) {
            request.file = input;
            downloadUrl = URL.createObjectURL(input);
            downloadName = input.name;
        } else if (typeof input === 'string') {
            request.url = input;
            downloadUrl = input;
            downloadName = input.split('/').pop();
        } else {
            return;
        }

        container.innerHTML = '';
        const mediaElement = previewChain.handle(request);
        if (mediaElement) {
            container.appendChild(mediaElement);
        }
        if (downloadUrl) {
            const dl = document.createElement('a');
            dl.href = downloadUrl;
            dl.download = downloadName;
            dl.textContent = 'Завантажити';
            dl.style.display = 'block';
            dl.style.marginTop = '8px';
            container.appendChild(dl);
        }
    };
})();
