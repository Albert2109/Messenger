(function () {
    
    class Handler {
        constructor() {
            this.next = null;
        }

        setNext(handler) {
            this.next = handler;
            return handler;
        }

        handle(request) {
            return this.next ? this.next.handle(request) : null;
        }
    }

    
    class ImageHandler extends Handler {
        canHandle({ file, url }) {
            return file
                ? file.type.startsWith('image/')
                : /\.(jpe?g|png|gif|bmp|webp)(\?.*)?$/i.test(url);
        }

        handle(request) {
            if (this.canHandle(request)) {
                const img = document.createElement('img');
                img.className = 'msg-img rounded';
                img.loading = 'lazy';
                img.src = request.file
                    ? URL.createObjectURL(request.file)
                    : request.url;
                return img;
            }
            return super.handle(request);
        }
    }

    
    class VideoHandler extends Handler {
        canHandle({ file, url }) {
            return file
                ? file.type.startsWith('video/')
                : /\.(mp4|webm|ogg)(\?.*)?$/i.test(url);
        }

        handle(request) {
            if (this.canHandle(request)) {
                const video = document.createElement('video');
                video.className = 'msg-video';
                video.controls = true;
                video.src = request.file
                    ? URL.createObjectURL(request.file)
                    : request.url;
                return video;
            }
            return super.handle(request);
        }
    }

    
    class AudioHandler extends Handler {
        canHandle({ file, url }) {
            return file
                ? file.type.startsWith('audio/')
                : /\.(mp3|wav|ogg|flac)(\?.*)?$/i.test(url);
        }

        handle(request) {
            if (this.canHandle(request)) {
                const audio = document.createElement('audio');
                audio.className = 'msg-audio';
                audio.controls = true;
                audio.src = request.file
                    ? URL.createObjectURL(request.file)
                    : request.url;
                return audio;
            }
            return super.handle(request);
        }
    }

    
    class DefaultHandler extends Handler {
        handle(request) {
            const link = document.createElement('a');
            const href = request.url || URL.createObjectURL(request.file);
            const name = request.file
                ? request.file.name
                : request.url.split('/').pop();
            link.href = href;
            link.download = name;
            link.textContent = name;
            link.className = 'btn btn-sm btn-outline-primary mt-2';
            return link;
        }
    }

   
    const previewChain = new ImageHandler();
    previewChain
        .setNext(new VideoHandler())
        .setNext(new AudioHandler())
        .setNext(new DefaultHandler());

   
    window.preview = function (input, container) {
        if (!container) return;
        let request = {};
        if (input instanceof File) {
            request = { file: input };
        } else if (typeof input === 'string') {
            request = { url: input };
        } else {
            return;
        }

        container.innerHTML = '';
        const element = previewChain.handle(request);
        if (element) container.appendChild(element);
    };
})();