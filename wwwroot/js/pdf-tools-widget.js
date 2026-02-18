/**
 * PDF Tools Widget for Dashboard - Enhanced Version
 * Handles PDF processing, favorites, storage stats, and history
 */

// --- State ---
let pdfCurrentTab = 'merge';
let pdfSelectedFiles = [];
let pdfCurrentDownloadUrl = "";
let pdfFavorites = [];
let pdfStorageUsage = 0;
let pdfStorageLimit = 524288000; // 500 MB in bytes

// --- DOM Elements ---
let pdfFileList, pdfActionBtn, pdfSpinner, pdfResultSection, pdfDropZone;

// Initialize when panel is opened
function initPdfToolsWidget() {
    console.log("PDF Tools Widget: Initializing...");

    // Get DOM elements
    pdfFileList = document.getElementById('pdf-file-list');
    pdfActionBtn = document.getElementById('pdf-processBtn');
    pdfSpinner = document.getElementById('pdf-spinner');
    pdfResultSection = document.getElementById('pdf-result-section');
    pdfDropZone = document.getElementById('pdf-drop-zone');

    // Reset state
    pdfSelectedFiles = [];
    pdfCurrentTab = 'merge';
    updatePdfFileList();

    // Load user data
    loadPdfUserData();
    loadPdfHistory();
}

// --- Load User Data (Storage, Favorites, Auto-delete) ---
async function loadPdfUserData() {
    try {
        const response = await fetch('/PdfTools/GetUserData', {
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        });
        if (response.ok) {
            const data = await response.json();

            // Update storage
            pdfStorageUsage = data.storageUsage || 0;
            pdfStorageLimit = data.storageLimit || 524288000;
            updateStorageDisplay();

            // Update favorites
            pdfFavorites = data.favorites || [];
            updateFavoriteStars();

            // Update auto-delete toggle
            const autoDeleteToggle = document.getElementById('pdf-autoDeleteToggle');
            if (autoDeleteToggle) {
                autoDeleteToggle.checked = data.autoDeleteEnabled || false;
            }
        }
    } catch (error) {
        console.error('Error loading PDF user data:', error);
    }
}

function updateStorageDisplay() {
    const usageMB = (pdfStorageUsage / 1024 / 1024).toFixed(1);
    const limitMB = (pdfStorageLimit / 1024 / 1024).toFixed(0);
    const percent = (pdfStorageUsage / pdfStorageLimit) * 100;

    const displayEl = document.getElementById('pdf-storage-display');
    const barEl = document.getElementById('pdf-storage-bar');

    if (displayEl) displayEl.textContent = `${usageMB} / ${limitMB} MB`;
    if (barEl) {
        barEl.style.width = `${percent}%`;
        barEl.className = 'progress-bar ' + (percent > 90 ? 'bg-danger' : percent > 70 ? 'bg-warning' : 'bg-success');
    }
}

function updateFavoriteStars() {
    document.querySelectorAll('.favorite-star-widget').forEach(star => {
        const card = star.closest('.tool-list-item');
        const toolId = card?.getAttribute('data-tool');
        if (toolId && pdfFavorites.includes(toolId)) {
            star.classList.add('active');
        } else {
            star.classList.remove('active');
        }
    });
}

// --- Favorites ---
async function togglePdfFavorite(toolId, element) {
    try {
        const response = await fetch('/PdfTools/ToggleFavorite', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'X-Requested-With': 'XMLHttpRequest'
            },
            body: JSON.stringify({ toolId: toolId })
        });

        if (response.ok) {
            const result = await response.json();
            if (result.success) {
                const starEl = element.closest('.favorite-star-widget');
                if (starEl) {
                    starEl.classList.toggle('active');
                }
                // Update local favorites array
                if (pdfFavorites.includes(toolId)) {
                    pdfFavorites = pdfFavorites.filter(id => id !== toolId);
                } else {
                    pdfFavorites.push(toolId);
                }
            }
        }
    } catch (error) {
        console.error('Error toggling favorite:', error);
    }
}

// --- Auto-delete Toggle ---
async function togglePdfAutoDelete(checkbox) {
    try {
        const response = await fetch('/PdfTools/ToggleAutoDelete', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'X-Requested-With': 'XMLHttpRequest'
            },
            body: JSON.stringify({ enabled: checkbox.checked })
        });

        if (response.ok) {
            const result = await response.json();
            if (result.success) {
                showPdfToast('Auto-delete setting updated', 'success');
            }
        }
    } catch (error) {
        console.error('Error toggling auto-delete:', error);
        checkbox.checked = !checkbox.checked; // Revert on error
    }
}

// --- History ---
async function loadPdfHistory() {
    const loadingEl = document.getElementById('pdf-history-loading');
    const gridEl = document.getElementById('pdf-history-grid');
    const emptyEl = document.getElementById('pdf-history-empty');

    if (loadingEl) loadingEl.style.display = 'block';
    if (gridEl) gridEl.innerHTML = '';
    if (emptyEl) emptyEl.style.display = 'none';

    try {
        const response = await fetch('/PdfTools/GetHistory', {
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        });
        if (response.ok) {
            const data = await response.json();

            if (loadingEl) loadingEl.style.display = 'none';

            if (data && data.length > 0) {
                renderPdfHistory(data);
            } else {
                if (emptyEl) emptyEl.style.display = 'block';
            }
        }
    } catch (error) {
        console.error('Error loading PDF history:', error);
        if (loadingEl) loadingEl.style.display = 'none';
        if (emptyEl) emptyEl.style.display = 'block';
    }
}

function renderPdfHistory(items) {
    const gridEl = document.getElementById('pdf-history-grid');
    if (!gridEl) return;

    gridEl.innerHTML = items.map(item => `
        <div class="history-card-premium" data-id="${item.id}">
            <div class="history-card-header">
                <div class="history-file-icon">
                    <i class="fas fa-file-pdf"></i>
                </div>
                <span class="history-action-badge">${item.toolType}</span>
            </div>
            
            <div class="history-card-content mt-2">
                <div class="history-filename" title="${item.originalFileNames}">${item.originalFileNames}</div>
                <div class="history-date">
                    <i class="far fa-clock"></i>
                    ${new Date(item.createdAt).toLocaleDateString('en-US', { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' })}
                </div>
            </div>

            <div class="history-actions-row">
                <a href="${item.storedFilePath}" class="btn-history-download" download title="Download">
                    <i class="fas fa-download"></i> Download
                </a>
                <button class="btn-history-delete" onclick="deletePdfHistoryItem('${item.id}')" title="Delete">
                    <i class="fas fa-trash-alt"></i>
                </button>
            </div>
        </div>
    `).join('');
}

async function refreshPdfHistory() {
    await loadPdfHistory();
    showPdfToast('History refreshed', 'success');
}

async function deletePdfHistoryItem(id) {
    if (!confirm('Are you sure you want to delete this file?')) return;

    try {
        const response = await fetch(`/PdfTools/DeleteHistory/${id}`, {
            method: 'DELETE',
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        });

        if (response.ok) {
            showPdfToast('File deleted successfully', 'success');
            await loadPdfHistory();
            await loadPdfUserData(); // Refresh storage stats
        }
    } catch (error) {
        console.error('Error deleting history item:', error);
        showPdfToast('Failed to delete file', 'error');
    }
}

// --- Tab Switching ---
function switchPdfTab(tab) {
    try {
        pdfCurrentTab = tab;

        // Update Card Active State
        document.querySelectorAll('.tool-list-item').forEach(card => card.classList.remove('active'));
        const activeCard = document.querySelector(`.tool-list-item[data-tool="${tab}"]`);
        if (activeCard) activeCard.classList.add('active');

        // Trigger Fade In Animation
        const workspace = document.getElementById('pdf-workspace-widget');
        if (workspace) {
            workspace.classList.remove('fade-in');
            void workspace.offsetWidth; // Trigger reflow
            workspace.classList.add('fade-in');
        }

        // Update Titles and Descriptions
        const titles = {
            'merge': 'Merge PDF',
            'split': 'Split PDF',
            'images': 'Images to PDF',
            'compress': 'Compress PDF'
        };

        const descriptions = {
            'merge': 'Combine multiple PDFs into one unified document',
            'split': 'Extract pages or split extensive documents',
            'images': 'Convert JPG/PNG images into a PDF portfolio',
            'compress': 'Reduce file size while maintaining quality'
        };

        const titleEl = document.getElementById('pdf-workspace-title');
        const descEl = document.getElementById('pdf-workspace-desc');

        if (titleEl) titleEl.innerText = titles[tab] || 'PDF Tool';
        if (descEl) descEl.innerText = descriptions[tab] || '';

        // Show/Hide Options
        const splitOpt = document.getElementById('pdf-split-options');
        if (splitOpt) splitOpt.style.display = tab === 'split' ? 'block' : 'none';

        const compressOpt = document.getElementById('pdf-compress-options');
        if (compressOpt) compressOpt.style.display = tab === 'compress' ? 'block' : 'none';

        // Reset
        clearPdfFiles();

        // Update Input Attribute
        const fileInput = document.getElementById('pdfFileInput');
        if (fileInput) {
            if (tab === 'images') {
                fileInput.accept = '.jpg,.jpeg,.png';
                fileInput.multiple = true;
            } else if (tab === 'merge') {
                fileInput.accept = '.pdf';
                fileInput.multiple = true;
            } else {
                fileInput.accept = '.pdf';
                fileInput.multiple = false;
            }
        }
    } catch (e) {
        console.error("Error switching PDF tab:", e);
        showPdfToast("Error switching tool", "error");
    }
}

// --- Toasts ---
function showPdfToast(message, type = 'info', title = '') {
    const container = document.getElementById('pdf-toast-container');
    if (!container) return;

    const toast = document.createElement('div');
    toast.className = `toast-premium ${type}`;

    if (!title) {
        if (type === 'success') title = 'Success';
        else if (type === 'error') title = 'Error';
        else title = 'Notification';
    }

    const iconClass = type === 'success' ? 'fa-check-circle' : (type === 'error' ? 'fa-exclamation-circle' : 'fa-info-circle');

    toast.innerHTML = `
        <div class="toast-icon"><i class="fas ${iconClass}"></i></div>
        <div class="toast-content">
            <div class="toast-title">${title}</div>
            <div class="toast-message">${message}</div>
        </div>
    `;

    container.appendChild(toast);
    setTimeout(() => toast.classList.add('show'), 100);

    setTimeout(() => {
        toast.classList.remove('show');
        setTimeout(() => toast.remove(), 500);
    }, 4000);
}

// --- Drag & Drop ---
function handlePdfDragOver(e) {
    e.preventDefault();
    if (pdfDropZone) pdfDropZone.classList.add('drag-over');
}

function handlePdfDragLeave(e) {
    e.preventDefault();
    if (pdfDropZone) pdfDropZone.classList.remove('drag-over');
}

function handlePdfDrop(e) {
    e.preventDefault();
    if (pdfDropZone) pdfDropZone.classList.remove('drag-over');
    handlePdfFiles(e.dataTransfer.files);
}

function handlePdfFileSelect(e) {
    if (e.target && e.target.files) {
        handlePdfFiles(e.target.files);
    }
}

function handlePdfFiles(files) {
    const newFiles = Array.from(files);

    // Validation
    const invalid = newFiles.filter(f => {
        if (pdfCurrentTab === 'images') return !f.type.startsWith('image/');
        return f.type !== 'application/pdf';
    });

    if (invalid.length > 0) {
        showPdfToast(`Invalid file type(s) detected. Please upload ${pdfCurrentTab === 'images' ? 'images' : 'PDFs'}.`, 'error');
        return;
    }

    if (pdfCurrentTab === 'merge' || pdfCurrentTab === 'images') {
        pdfSelectedFiles = [...pdfSelectedFiles, ...newFiles];
    } else {
        pdfSelectedFiles = [newFiles[0]];
    }

    updatePdfFileList();
}

function clearPdfFiles() {
    pdfSelectedFiles = [];
    updatePdfFileList();
    if (pdfResultSection) pdfResultSection.classList.add('d-none');
}

function updatePdfFileList() {
    if (!pdfFileList) return;
    pdfFileList.innerHTML = '';
    pdfSelectedFiles.forEach((file, index) => {
        const li = document.createElement('li');
        li.className = 'file-item';
        li.innerHTML = `
            <div class="file-info">
                <i class="fas ${file.type.startsWith('image/') ? 'fa-image text-warning' : 'fa-file-pdf text-accent'} file-icon"></i>
                <div>
                    <div class="fw-bold">${file.name}</div>
                    <div class="text-muted small">${(file.size / 1024 / 1024).toFixed(2)} MB</div>
                </div>
            </div>
            <button class="remove-btn" onclick="removePdfFile(${index})"><i class="fas fa-times"></i></button>
        `;
        pdfFileList.appendChild(li);
    });
}

function removePdfFile(index) {
    pdfSelectedFiles.splice(index, 1);
    updatePdfFileList();
}

// --- Core Processing ---
async function processPdfFiles() {
    if (pdfSelectedFiles.length === 0) {
        showPdfToast("Please select files first", 'info');
        return;
    }

    const formData = new FormData();
    pdfSelectedFiles.forEach(f => formData.append('files', f));

    // Add options based on tab
    const pageRangeEl = document.getElementById('pdf-pageRange');
    const splitAllEl = document.getElementById('pdf-splitAll');
    const compressionLevelEl = document.getElementById('pdf-compressionLevel');

    if (pdfCurrentTab === 'split') {
        if (pageRangeEl) formData.append('pageRange', pageRangeEl.value);
        if (splitAllEl) formData.append('splitAll', splitAllEl.checked);
    } else if (pdfCurrentTab === 'compress') {
        if (compressionLevelEl) formData.append('compressionLevel', compressionLevelEl.value);
    }

    try {
        // UI State
        if (pdfActionBtn) pdfActionBtn.disabled = true;
        if (pdfSpinner) pdfSpinner.style.display = 'block';
        if (pdfResultSection) pdfResultSection.classList.add('d-none');

        const progressContainer = document.getElementById('pdf-progress-container');
        const progressBar = document.getElementById('pdf-progress-bar');
        const progressPercent = document.getElementById('pdf-progress-percent');
        const progressLabel = document.getElementById('pdf-progress-label');

        if (progressContainer) progressContainer.classList.remove('d-none');
        if (progressBar) progressBar.style.width = '0%';
        if (progressPercent) progressPercent.innerText = '0%';
        if (progressLabel) progressLabel.innerText = 'Uploading...';

        const xhr = new XMLHttpRequest();
        let endpoint = '';
        switch (pdfCurrentTab) {
            case 'merge': endpoint = '/PdfTools/MergePdf'; break;
            case 'split': endpoint = '/PdfTools/SplitPdf'; break;
            case 'images': endpoint = '/PdfTools/ImagesToPdf'; break;
            case 'compress': endpoint = '/PdfTools/CompressPdf'; break;
        }

        xhr.upload.onprogress = (e) => {
            if (e.lengthComputable) {
                const percent = Math.round((e.loaded / e.total) * 100);
                if (progressBar) progressBar.style.width = percent + '%';
                if (progressPercent) progressPercent.innerText = percent + '%';
                if (percent === 100 && progressLabel) progressLabel.innerText = 'Processing on server...';
            }
        };

        xhr.onload = () => {
            if (pdfActionBtn) pdfActionBtn.disabled = false;
            if (pdfSpinner) pdfSpinner.style.display = 'none';
            if (progressContainer) progressContainer.classList.add('d-none');

            if (xhr.status === 200) {
                const res = JSON.parse(xhr.responseText);
                if (res.success) {
                    showPdfToast("Processing complete!", 'success');
                    if (pdfResultSection) pdfResultSection.classList.remove('d-none');
                    const downloadLink = document.getElementById('pdf-download-link');
                    if (downloadLink) downloadLink.href = res.fileUrl;
                    pdfCurrentDownloadUrl = res.fileUrl;

                    // Refresh history and storage
                    setTimeout(() => {
                        loadPdfHistory();
                        loadPdfUserData();
                    }, 1000);
                } else {
                    showPdfToast(res.message || "Failed to process", 'error');
                }
            } else {
                showPdfToast("Server error occurred", 'error');
            }
        };

        xhr.onerror = () => {
            showPdfToast("Network error", 'error');
            if (pdfActionBtn) pdfActionBtn.disabled = false;
            if (pdfSpinner) pdfSpinner.style.display = 'none';
            if (progressContainer) progressContainer.classList.add('d-none');
        };

        xhr.open('POST', endpoint);
        xhr.setRequestHeader('X-Requested-With', 'XMLHttpRequest');
        xhr.send(formData);

    } catch (e) {
        console.error("Process error:", e);
        showPdfToast("An error occurred during processing", 'error');
        if (pdfActionBtn) pdfActionBtn.disabled = false;
        if (pdfSpinner) pdfSpinner.style.display = 'none';
    }
}

// Make functions globally available
window.switchPdfTab = switchPdfTab;
window.handlePdfDragOver = handlePdfDragOver;
window.handlePdfDragLeave = handlePdfDragLeave;
window.handlePdfDrop = handlePdfDrop;
window.handlePdfFileSelect = handlePdfFileSelect;
window.clearPdfFiles = clearPdfFiles;
window.removePdfFile = removePdfFile;
window.processPdfFiles = processPdfFiles;
window.initPdfToolsWidget = initPdfToolsWidget;
window.togglePdfFavorite = togglePdfFavorite;
window.togglePdfAutoDelete = togglePdfAutoDelete;
window.refreshPdfHistory = refreshPdfHistory;
window.deletePdfHistoryItem = deletePdfHistoryItem;

// Initialize when the PDF tools panel is opened
document.addEventListener('DOMContentLoaded', () => {
    // Listen for when the PDF tools panel becomes active
    const observer = new MutationObserver((mutations) => {
        mutations.forEach((mutation) => {
            if (mutation.target.id === 'pdftoolsDetails' && mutation.target.classList.contains('active')) {
                initPdfToolsWidget();
            }
        });
    });

    const pdfPanel = document.getElementById('pdftoolsDetails');
    if (pdfPanel) {
        observer.observe(pdfPanel, { attributes: true, attributeFilter: ['class'] });
    }
});
