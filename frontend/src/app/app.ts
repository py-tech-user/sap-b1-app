import { Component, OnInit, signal, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { environment } from '../environments/environment';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, CommonModule],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App implements OnInit {
  readonly isDemoMode = signal(false);
  private readonly http = inject(HttpClient);

  ngOnInit() {
    this.checkConfig();
  }

  private checkConfig() {
    this.http.get<{isDemoMode: boolean}>(`${environment.apiUrl}/sap/config`).subscribe({
      next: (config) => this.isDemoMode.set(config.isDemoMode),
      error: () => this.isDemoMode.set(false)
    });
  }
}
