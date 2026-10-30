import { HttpClient, HttpResponse } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from './api-base-url';
import { CreatePersonRequest, PersonResponse } from './person.models';

@Injectable({ providedIn: 'root' })
export class PersonApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  create(request: CreatePersonRequest): Observable<HttpResponse<PersonResponse>> {
    return this.http.post<PersonResponse>(`${this.baseUrl}/persons`, request, {
      observe: 'response',
    });
  }
}
