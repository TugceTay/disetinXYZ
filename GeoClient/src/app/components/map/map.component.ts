import { DataService } from './../../data.service';
import { Component, OnInit, AfterViewInit } from '@angular/core';
import * as L from 'leaflet';
import 'leaflet-draw';
import 'leaflet-control-geocoder';
import { Chart, Plugin, ArcElement, DoughnutController, Tooltip, Legend } from 'chart.js';

Chart.register(ArcElement, DoughnutController, Tooltip, Legend);

interface WMS {
  id: string;
  name: string;
  visible: boolean;
  legendUrl: string;
  layer?: L.TileLayer.WMS;
}

@Component({
  selector: 'app-map',
  templateUrl: './map.component.html',
  styleUrls: ['./map.component.css']
})
export class MapComponent implements OnInit, AfterViewInit {

  private map: L.Map | null;
  private drawControl: L.Control.Draw | null = null;
  private drawnItems: L.FeatureGroup;
  private earthquakeChart: Chart<'doughnut'> | null = null;
  private floodChart: Chart<'doughnut'> | null = null;
  private landslideChart: Chart<'doughnut'> | null = null;
  private baseLayers: { [key: string]: L.TileLayer } = {};
  private zoomControl: L.Control | null = null;
  private warningPopup: L.Popup | null = null;

  private currentParameters: any = {};
  private currentRiskValues: any = {};
  private currentRiskData: any = {}; 

  public isSidebarClosed = false;
  public wmsLayers: WMS[] = [
    { id: 'layer2', name: 'Çölleşme Hassasiyeti', visible: false, legendUrl: 'https://tucbs-public-api.csb.gov.tr/trk_cemgm_collesmehassasiyetharitasi_wms?service=WMS&request=GetLegendGraphic&format=image/png&layer=0&version=1.3.0', layer: L.tileLayer.wms('https://tucbs-public-api.csb.gov.tr/trk_cemgm_collesmehassasiyetharitasi_wms', { layers: '0', format: 'image/png', transparent: true }) },
    { id: 'layer3', name: 'Toprağın Erozyona Duyarlılığı', visible: false, legendUrl: 'https://tucbs-public-api.csb.gov.tr/trk_cemgm_topragin_erozyona_duyarliligi_faktoru_wms?service=WMS&request=GetLegendGraphic&format=image/png&layer=0&version=1.3.0', layer: L.tileLayer.wms('https://tucbs-public-api.csb.gov.tr/trk_cemgm_topragin_erozyona_duyarliligi_faktoru_wms', { layers: '0', format: 'image/png', transparent: true }) },
    { id: 'layer4', name: 'Su Erozyonu', visible: false, legendUrl: 'https://tucbs-public-api.csb.gov.tr/trk_cemgm_su_erozyonu_wms?service=WMS&request=GetLegendGraphic&format=image/png&layer=0&version=1.3.0', layer: L.tileLayer.wms('https://tucbs-public-api.csb.gov.tr/trk_cemgm_su_erozyonu_wms', { layers: '0', format: 'image/png', transparent: true }) },
  ];
  
  constructor(private dataService: DataService) {
    this.map = null;
    this.drawnItems = new L.FeatureGroup();
  }

  private createZoomControl(): void {
    if (!this.map) return;

    const ZoomControl = L.Control.extend({
      onAdd: () => {
        const container = L.DomUtil.create('div', 'leaflet-bar zoom-level-control');
        container.innerHTML = `Zoom Seviyesi: ${this.map?.getZoom()}`;
        return container;
      }
    });

    this.zoomControl = new ZoomControl({ position: 'bottomleft' });
    this.zoomControl.addTo(this.map);
  }

  private updateZoomControl(): void {
    const container = document.querySelector('.zoom-level-control');
    if (container) {
      container.innerHTML = `Zoom Seviyesi: ${this.map?.getZoom()}`;
    }
  }

  private isZoomLevelValid(): boolean {
    if (!this.map) return false;
    const zoom = this.map.getZoom();
    return zoom >= 17 ;
  }

  private showZoomWarning(): void {
    if (!this.map) return;

    if (!this.warningPopup) {
      const popupContent = document.createElement('div');
      popupContent.innerHTML = '<p>Çizim yapabilmek için gerekli zoom seviyesi 17 ve üzeri olmalıdır.</p>';
      this.warningPopup = L.popup({ className: 'warning-popup' }) 
        .setLatLng(this.map.getCenter())
        .setContent(popupContent)
        .openOn(this.map);
    }
  }

  private hideZoomWarning(): void {
    if (this.map && this.warningPopup) {
      this.map.closePopup(this.warningPopup);
      this.warningPopup = null;
    }
  }

  ngOnInit(): void {
    this.initMap();
    this.isSidebarClosed = false;
  
    const questionBtn = document.getElementById('questionBtn');
    questionBtn?.addEventListener('click', () => {
      this.showQuestionPopup();
    });
  
    const closeQuestionPopup = document.getElementById('closeQuestionPopup');
    closeQuestionPopup?.addEventListener('click', () => {
      this.hideQuestionPopup();
    });
  
    const bookBtn = document.getElementById('bookBtn');
    bookBtn?.addEventListener('click', () => {
      this.showbookPopup();
    });
  
    const closebookPopup = document.getElementById('closebookPopup');
    closebookPopup?.addEventListener('click', () => {
      this.hidebookPopup();
    });

    this.currentParameters = {
      zeminSinifi: '',
      toprakTipi: '',
      yagis: '',
      bitkiOrtusu: '',
      litoloji: '',
      slopePercentage: '',
    };

    this.initMap();
    this.isSidebarClosed = false;
  }
  
  showQuestionPopup() {
    const popup = document.getElementById('questionPopup');
    if (popup) {
      popup.classList.remove('hidden');
      popup.style.display = 'block';
    }
  }
  
  hideQuestionPopup() {
    const popup = document.getElementById('questionPopup');
    if (popup) {
      popup.classList.add('hidden');
      popup.style.display = 'none';
    }
  }
  
  showbookPopup() {
    const popup = document.getElementById('bookPopup');
    if (popup) {
      popup.classList.remove('hidden');
      popup.style.display = 'block';
    }
  }
  
  hidebookPopup() {
    const popup = document.getElementById('bookPopup');
    if (popup) {
      popup.classList.add('hidden');
      popup.style.display = 'none';
    }
  }

  ngAfterViewInit(): void {
    if (this.map) {
      this.drawnItems.addTo(this.map);
    } else {
      //console.error("map is null");
    }

    this.initializeCharts();
    this.isSidebarClosed = false;
    this.addInfoButtonEventListeners();
    this.addMapButtonsEventListeners();

    const drawPolygonBtn = document.getElementById('drawPolygonBtn');
    drawPolygonBtn?.addEventListener('click', () => {
        if (this.map) {
            if (this.isZoomLevelValid()) {
                this.drawnItems.clearLayers();
                this.resetCharts();
                this.resetTableValues();
    
                this.dataService.clearMemory().subscribe((response: any) => {
                  //  console.log('Hafıza temizlendi');
                }, (error: any) => {
                  //  console.error('Hafıza temizlenirken hata oluştu:', error);
                });
    
                const drawPolygonHandler = new (L.Draw.Polygon as any)(this.map, {
                    shapeOptions: {
                        color: '#0B92FC',
                        weight: 8,
                        opacity: 1,
                    }
                });
                drawPolygonHandler.enable();
            } else {
                this.showZoomWarning();
            }
        } else {
           // console.error("map is null");
        }
    });

    if (this.map) {
        this.map.on('zoomend', () => {
            this.updateZoomControl();
            if (this.isZoomLevelValid()) {
                this.hideZoomWarning();
            } else {
                this.showZoomWarning();
            }
        });
    }
    
    window.addEventListener('resize', () => {
      if (this.earthquakeChart) {
        this.earthquakeChart.resize();
      }
      if (this.floodChart) {
        this.floodChart.resize();
      }
      if (this.landslideChart) {
        this.landslideChart.resize();
      }
    });

    const distanceToFaultPen = document.querySelector('#distanceToFaultBtn + .fa-pen');
    const slopeDegreePen = document.querySelector('#slopeDegreeBtn + .fa-pen');
    const elevationPen = document.querySelector('#elevationBtn + .fa-pen');
    const distanceToWetlandsPen = document.querySelector('#distanceToWetlandsBtn + .fa-pen');
    
    distanceToFaultPen?.addEventListener('click', () => {
      const distanceToFaultValue = document.getElementById('distanceToFaultValue');
      distanceToFaultValue?.setAttribute('contenteditable', 'true');
      distanceToFaultValue?.focus();
      this.showCheckMark('#distanceToFaultBtn + .fa-pen');
    });
    
    slopeDegreePen?.addEventListener('click', () => {
        const slopeDegreeValue = document.getElementById('slopeDegreeValue');
        slopeDegreeValue?.setAttribute('contenteditable', 'true');
        slopeDegreeValue?.focus();
        this.showCheckMark('#slopeDegreeBtn + .fa-pen');
    });
    
    elevationPen?.addEventListener('click', () => {
      const elevationValue = document.getElementById('elevationValue');
      elevationValue?.setAttribute('contenteditable', 'true');
      elevationValue?.focus();
      this.showCheckMark('#elevationBtn + .fa-pen');
    });
    
    distanceToWetlandsPen?.addEventListener('click', () => {
      const distanceToWetlandsValue = document.getElementById('distanceToWetlandsValue');
      distanceToWetlandsValue?.setAttribute('contenteditable', 'true');
      distanceToWetlandsValue?.focus();
      this.showCheckMark('#distanceToWetlandsBtn + .fa-pen');
    });
}

private addInfoButtonEventListeners(): void {
  const infoButtons = [
    { btnId: 'distanceToFaultBtn', popupId: 'distanceToFaultPopup' },
    { btnId: 'slopeDegreeBtn', popupId: 'slopeDegreePopup' },
    { btnId: 'soilClassBtn', popupId: 'soilClassPopup' },
    { btnId: 'elevationBtn', popupId: 'elevationPopup' },
    { btnId: 'soilTypeBtn', popupId: 'soilTypePopup' },
    { btnId: 'rainfallBtn', popupId: 'rainfallPopup' },
    { btnId: 'distanceToWetlandsBtn', popupId: 'distanceToWetlandsPopup' },
    { btnId: 'vegetationCoverBtn', popupId: 'vegetationCoverPopup' },
    { btnId: 'litolojiBtn', popupId: 'litolojiPopup' }
  ];

  infoButtons.forEach(info => {
    const btn = document.getElementById(info.btnId);
    const popup = document.getElementById(info.popupId);

    btn?.addEventListener('click', (event) => {
      if (popup) {
        const rect = (event.target as HTMLElement).getBoundingClientRect();
        popup.style.top = `${rect.top}px`;
        popup.style.left = `${rect.right + 10}px`;
        popup.classList.remove('hidden');
        popup.style.display = 'block';
      }
    });
  });

  window.addEventListener('click', (event) => {
    infoButtons.forEach(info => {
      const popup = document.getElementById(info.popupId);
      const btn = document.getElementById(info.btnId);
      if (popup && !popup.contains(event.target as Node) && btn !== event.target) {
        popup.classList.add('hidden');
        popup.style.display = 'none';
      }
    });
  });
}

private showCheckMark(selector: string) {
  const penElement = document.querySelector(selector);
  if (penElement) {
      const checkMark = document.createElement('i');
      checkMark.classList.add('fas', 'fa-check');
      checkMark.style.color = 'green';
      checkMark.style.marginLeft = '5px';
      checkMark.style.fontWeight = 'bold';
      checkMark.style.fontSize = '20px';

      penElement.parentElement?.appendChild(checkMark);

      checkMark.addEventListener('click', () => {
          const valueElement = penElement.parentElement?.nextElementSibling;
          if (valueElement) {
              const newValue = valueElement.textContent?.trim() || null;
             // console.log('Yeni Değer: ', newValue);
              valueElement.setAttribute('contenteditable', 'false');
              checkMark.remove();

              this.updateBackend(valueElement.id, newValue);
          }
      });
  }
}


private updateRiskResults(response: any) {
  this.currentRiskValues = { ...this.currentRiskValues, ...response };

  const earthquakeRiskValue = document.getElementById('earthquakeRiskValue');
  const floodRiskValue = document.getElementById('floodRiskValue');
  const landslideRiskValue = document.getElementById('landslideRiskValue');

  if (earthquakeRiskValue) {
      earthquakeRiskValue.innerText = response.earthquakeRisk || this.currentRiskValues.earthquakeRisk;
  }

  if (floodRiskValue) {
      floodRiskValue.innerText = response.floodRisk || this.currentRiskValues.floodRisk;
  }

  if (landslideRiskValue) {
      landslideRiskValue.innerText = response.landslideRisk || this.currentRiskValues.landslideRisk;
  }

  this.updateCharts(response);
}

private updateBackend(parameterId: string, newValue: string | null) {
  if (newValue !== null) {
    if (parameterId === 'distanceToWetlandsValue') {
      this.currentParameters['wetlandDistance'] = newValue;
    } else {
      this.currentParameters[parameterId] = newValue;
    }

    const payload = {
      properties: this.currentParameters
    };
    
    //console.log("guncel parametreler", JSON.stringify(payload));

    this.dataService.calculateRisk(payload).subscribe(response => {
      //console.log('guncel sonuc', response);
      this.updateRiskResults(response);
    }, error => {
      //console.error(error);
    });
  }
}

private initMap(): void {
  this.map = L.map('map', { zoomControl: false }).setView([39.2, 33], 7);
  this.baseLayers = {
    'Standard': L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', { attribution: 'Standard' }),
    'Satellite Streets': L.tileLayer('', { attribution: 'Satellite Streets' }),
    'Outdoors': L.tileLayer('', { attribution: 'Outdoors' }),
    'Light': L.tileLayer('', { attribution: 'Light' }),
    'Dark': L.tileLayer('', { attribution: 'Dark' }),
    'OSM': L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', { attribution: 'OSM' })
  };
  this.baseLayers['Satellite Streets'].addTo(this.map);

  const geocoder = (L.Control as any).geocoder({
    defaultMarkGeocode: false,
    position: 'topright',
    collapsed: false
  })
    .on('markgeocode', (e: any) => {
      const center = e.geocode.center;
      this.map?.setView(center, 17);
    })
    .addTo(this.map);

  L.control.zoom({ position: 'topright' }).addTo(this.map);
  this.drawnItems = new L.FeatureGroup().addTo(this.map);

  this.createZoomControl();

  this.map.on(L.Draw.Event.CREATED, (event: any) => {
    const layer = event.layer;
    this.drawnItems.addLayer(layer);
    const drawnGeometry = layer.toGeoJSON();
    console.log("gönderilen geojson: ", JSON.stringify(drawnGeometry));

    const popupContent = document.createElement('div');
    popupContent.innerHTML = 
      `<label>Zemin Sınıfı:</label>
      <select id="zeminSinifi">
          <option value="ZA">ZA: Kaya</option>
          <option value="ZB">ZB: Sağlam zemin</option>
          <option value="ZC">ZC: Orta zemin</option>
          <option value="ZD">ZD: Zayıf zemin</option>
          <option value="ZE">ZE: Çok zayıf zemin</option>
          <option value="ZF">ZF: Dolgu</option>
      </select><br>
      <label>Toprak Tipi:</label>
      <select id="toprakTipi">
          <option value="Organik Topraklar">Organik Topraklar</option>
          <option value="Killi Topraklar">Killi Topraklar</option>
          <option value="Siltli Topraklar">Siltli Topraklar</option>
          <option value="Kumlu Topraklar">Kumlu Topraklar</option>
          <option value="Çakıllı Topraklar">Çakıllı Topraklar</option>
      </select><br>
      <label>Yağış:</label>
      <select id="yagis">
          <option value="Çok Düşük Yağış">Çok Düşük Yağış</option>
          <option value="Düşük Yağış">Düşük Yağış</option>
          <option value="Orta Yağış">Orta Yağış</option>
          <option value="Yüksek Yağış">Yüksek Yağış</option>
          <option value="Çok Yüksek Yağış">Çok Yüksek Yağış</option>
      </select><br>
      <label>Bitki Örtüsü:</label>
      <select id="bitkiOrtusu">
          <option value="Kentsel Alanlar">Kentsel Alanlar</option>
          <option value="Tarım Alanları">Tarım Alanları</option>
          <option value="Otlaklar/Çayırlar">Otlaklar/Çayırlar</option>
          <option value="Çalılık Alanlar">Çalılık Alanlar</option>
          <option value="Ormanlık Alanlar">Ormanlık Alanlar</option>
      </select><br>
      <label>Litoloji:</label>
      <select id="litoloji">
          <option value="Alüvyon">Alüvyon</option>
          <option value="Kiltaşı">Kiltaşı</option>
          <option value="Çakıltaşı">Çakıltaşı</option>
          <option value="Kumtaşı">Kumtaşı</option>
          <option value="Kireçtaşı">Kireçtaşı</option>
          <option value="Bazalt">Bazalt</option>
          <option value="Granit">Granit</option>
      </select><br>
      <button id="calculateRiskBtn">Hesapla</button>`;

    popupContent.querySelector('#calculateRiskBtn')?.addEventListener('click', () => {
      this.calculateRisk(layer, drawnGeometry);
    });

    layer.bindPopup(popupContent).openPopup();
  });
}

public toggleLayer(layer: WMS): void {
  if (layer.layer) {
    layer.visible = !layer.visible;
    if (layer.visible) {
      this.map?.addLayer(layer.layer);
    } else {
      this.map?.removeLayer(layer.layer);
    }
  }
}

public toggleSidebar(): void {
  this.isSidebarClosed = !this.isSidebarClosed;
}

private addMapButtonsEventListeners(): void {
  document.getElementById('standard')?.addEventListener('click', () => this.changeBaseLayer(this.baseLayers['Standard']));
  document.getElementById('satellite-streets')?.addEventListener('click', () => this.changeBaseLayer(this.baseLayers['Satellite Streets']));
  document.getElementById('outdoors')?.addEventListener('click', () => this.changeBaseLayer(this.baseLayers['Outdoors']));
  document.getElementById('light')?.addEventListener('click', () => this.changeBaseLayer(this.baseLayers['Light']));
  document.getElementById('dark')?.addEventListener('click', () => this.changeBaseLayer(this.baseLayers['Dark']));
  document.getElementById('osm')?.addEventListener('click', () => this.changeBaseLayer(this.baseLayers['OSM']));
}

private changeBaseLayer(layer: L.TileLayer): void {
  if (this.map) {
    this.map.eachLayer(function (l) {
      if (l instanceof L.TileLayer) {
        l.remove();
      }
    });
    layer.addTo(this.map);
  }
}

private calculateRisk(layer: L.Layer, drawnGeometry: any): void {
  const zeminSinifi = (document.getElementById('zeminSinifi') as HTMLSelectElement).value;
  const toprakTipi = (document.getElementById('toprakTipi') as HTMLSelectElement).value;
  const yagis = (document.getElementById('yagis') as HTMLSelectElement).value;
  const bitkiOrtusu = (document.getElementById('bitkiOrtusu') as HTMLSelectElement).value;
  const litoloji = (document.getElementById('litoloji') as HTMLSelectElement).value;

  if (!zeminSinifi || !toprakTipi || !yagis || !bitkiOrtusu || !litoloji) {
      return;
  }

  const geoJsonPayload = {
    type: "Feature",
    geometry: drawnGeometry.geometry,
    properties: {
      zeminSinifi,
      toprakTipi,
      yagis,
      bitkiOrtusu,
      litoloji
    }
  };
 // console.log('gönderilen jSON:', JSON.stringify(geoJsonPayload)); 
  

  this.dataService.calculateRisk(geoJsonPayload).subscribe((riskResult) => {
   // console.log('risk resault:', riskResult);
    
    (document.getElementById('distanceToFaultValue') as HTMLElement).innerText = `${riskResult.faultDistance.toFixed(3)}`;
    (document.getElementById('slopeDegreeValue') as HTMLElement).innerText = `${riskResult.slopePercentage.toFixed(3)}`;
    (document.getElementById('soilClassValue') as HTMLElement).innerText = `${riskResult.zeminSinifi}`;
    (document.getElementById('elevationValue') as HTMLElement).innerText = `${riskResult.height.toFixed(3)}`;
    (document.getElementById('soilTypeValue') as HTMLElement).innerText = `${riskResult.toprakTipi}`;
    (document.getElementById('rainfallValue') as HTMLElement).innerText = `${riskResult.yagis}`;
    (document.getElementById('distanceToWetlandsValue') as HTMLElement).innerText = `${parseFloat(riskResult.wetlandDistance).toFixed(3)}`;
    (document.getElementById('vegetationCoverValue') as HTMLElement).innerText = `${riskResult.bitkiOrtusu}`;
    (document.getElementById('litolojiValue') as HTMLElement).innerText = `${riskResult.litoloji}`;

    this.updateCharts(riskResult);
    this.updateRiskResults(riskResult);
    layer.closePopup();
  }, (error) => {
  // console.error("hata ", error);
  });
}

private updateCharts(riskResult: any) {
  const earthquakeRisk = parseFloat(riskResult.earthquakeRisk);
  const floodRisk = parseFloat(riskResult.floodRisk);
  const landslideRisk = parseFloat(riskResult.landslideRisk);

  if (this.earthquakeChart) {
    this.earthquakeChart.data.datasets[0].data = [earthquakeRisk, 100 - earthquakeRisk];
    if (this.earthquakeChart.options.plugins) {
      (this.earthquakeChart.options.plugins as any).centerText = { text: `${earthquakeRisk.toFixed(2)}%` };
    }
    this.earthquakeChart.update();
  }

  if (this.floodChart) {
    this.floodChart.data.datasets[0].data = [floodRisk, 100 - floodRisk];
    if (this.floodChart.options.plugins) {
      (this.floodChart.options.plugins as any).centerText = { text: `${floodRisk.toFixed(2)}%` };
    }
    this.floodChart.update();
  }

  if (this.landslideChart) {
    this.landslideChart.data.datasets[0].data = [landslideRisk, 100 - landslideRisk];
    if (this.landslideChart.options.plugins) {
      (this.landslideChart.options.plugins as any).centerText = { text: `${landslideRisk.toFixed(2)}%` };
    }
    this.landslideChart.update();
  }
}

private initializeCharts() {
  const centerTextPlugin: Plugin = {
    id: 'centerText',
    beforeDraw(chart) {
      const ctx = chart.ctx;
      const width = chart.width;
      const height = chart.height;
      const plugins = chart.config.options?.plugins;
      if (plugins) {
        const centerText = (plugins as any).centerText;
        if (centerText) {
          const text = centerText.text || '';
          ctx.restore();
          ctx.font = '16px Arial';
          ctx.textBaseline = 'middle';
          ctx.textAlign = 'center';
          ctx.fillStyle = 'white';
          ctx.fillText(text, width / 2, height / 2);
          ctx.save();
        }
      }
    }
  };

  Chart.register(centerTextPlugin);

  const legendOptions = {
    labels: {
      font: {
        size: 16,
        family: 'Arial'
      },
      color: 'white'
    }
  };

  const earthquakeCtx = document.getElementById('earthquakeRiskCanvas') as HTMLCanvasElement;
  this.earthquakeChart = new Chart(earthquakeCtx, {
    type: 'doughnut',
    data: {
      labels: ['Deprem Tehlikesi'],
      datasets: [{
        data: [0, 100],
        backgroundColor: ['#0B92FC', '#eeeeee'],
        borderColor: '#ffffff',
        borderWidth: 1,
      }]
    },
    options: {
      plugins: {
        centerText: { text: '0%' },
        legend: legendOptions
      } as any
    }
  });

  const floodCtx = document.getElementById('floodRiskCanvas') as HTMLCanvasElement;
  this.floodChart = new Chart(floodCtx, {
    type: 'doughnut',
    data: {
      labels: ['Sel Tehlikesi'],
      datasets: [{
        data: [0, 100],
        backgroundColor: ['#FEB21F', '#eeeeee'],
        borderColor: '#ffffff',
        borderWidth: 1,
      }]
    },
    options: {
      plugins: {
        centerText: { text: '0%' },
        legend: legendOptions
      } as any
    }
  });

  const landslideCtx = document.getElementById('landslideRiskCanvas') as HTMLCanvasElement;
  this.landslideChart = new Chart(landslideCtx, {
    type: 'doughnut',
    data: {
      labels: ['Heyelan Tehlikesi'],
      datasets: [{
        data: [0, 100],
        backgroundColor: ['#0DE39A', '#eeeeee'],
        borderColor: '#ffffff',
        borderWidth: 1,
      }]
    },
    options: {
      plugins: {
        centerText: { text: '0%' },
        legend: legendOptions
      } as any
    }
  });
}

private resetCharts(): void {
  if (this.earthquakeChart) {
      this.earthquakeChart.data.datasets[0].data = [0, 100];
      const plugins = this.earthquakeChart.options.plugins as any;
      if (plugins) {
          plugins.centerText = { text: '0%' };
      }
      this.earthquakeChart.update();
  }

  if (this.floodChart) {
      this.floodChart.data.datasets[0].data = [0, 100];
      const plugins = this.floodChart.options.plugins as any;
      if (plugins) {
          plugins.centerText = { text: '0%' };
      }
      this.floodChart.update();
  }

  if (this.landslideChart) {
      this.landslideChart.data.datasets[0].data = [0, 100];
      const plugins = this.landslideChart.options.plugins as any;
      if (plugins) {
          plugins.centerText = { text: '0%' };
      }
      this.landslideChart.update();
  }
}

private resetTableValues(): void {
  (document.getElementById('distanceToFaultValue') as HTMLElement).innerText = '';
  (document.getElementById('slopeDegreeValue') as HTMLElement).innerText = '';
  (document.getElementById('soilClassValue') as HTMLElement).innerText = '';
  (document.getElementById('elevationValue') as HTMLElement).innerText = '';
  (document.getElementById('soilTypeValue') as HTMLElement).innerText = '';
  (document.getElementById('rainfallValue') as HTMLElement).innerText = '';
  (document.getElementById('distanceToWetlandsValue') as HTMLElement).innerText = '';
  (document.getElementById('vegetationCoverValue') as HTMLElement).innerText = '';
  (document.getElementById('litolojiValue') as HTMLElement).innerText = '';
}

showPopup() {
  const popup = document.getElementById('popup');
  if (popup) {
    popup.classList.remove('hidden');
    popup.style.display = 'block';
  }
}

hidePopup() {
  const popup = document.getElementById('popup');
  if (popup) {
    popup.classList.add('hidden');
    popup.style.display = 'none';
  }
}}
