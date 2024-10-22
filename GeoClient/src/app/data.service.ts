import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class DataService {

  constructor(private http: HttpClient) { }

  calculateSlope(geoJson: any): Observable<any> {
    return this.http.post('https://disetinapi.azurewebsites.net/api/Raster/CalculateSlope', geoJson);
  }

  calculateHeight(geoJson: any): Observable<any> {
    return this.http.post('https://disetinapi.azurewebsites.net/api/Raster/CalculateHeight', geoJson);
  }

  calculateNearestFault(geoJson: any): Observable<any> {
    return this.http.post('https://disetinapi.azurewebsites.net/api/FaultLine/CalculateNearestFault', geoJson);
  }

  calculateNearestWetland(geoJson: any): Observable<any> {
    return this.http.post('https://disetinapi.azurewebsites.net/api/Wetland/CalculateNearestWetland', geoJson);
  }

  calculateRisk(request: any): Observable<any> {
    return this.http.post('https://disetinapi.azurewebsites.net/api/Risk/CalculateRisk', request);
  }// YENİ SATIR: Hafızayı temizlemek için backend API çağrısı
clearMemory(): Observable<any> {
  return this.http.post('https://disetinapi.azurewebsites.net/api/Risk/ClearMemory', {});// Boş bir POST isteği gönderiyoruz
}

}
