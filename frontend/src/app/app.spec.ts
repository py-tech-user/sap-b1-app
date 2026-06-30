import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it } from 'vitest';
import { App } from './app';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([])
      ]
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const http = TestBed.inject(HttpTestingController);
    http.expectOne(request => request.url.endsWith('/sap/config')).flush({ isDemoMode: false });
    http.verify();

    expect(fixture.componentInstance).toBeTruthy();
  });

  it('should render the router outlet', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();

    const http = TestBed.inject(HttpTestingController);
    http.expectOne(request => request.url.endsWith('/sap/config')).flush({ isDemoMode: false });
    http.verify();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('router-outlet')).toBeTruthy();
  });
});
