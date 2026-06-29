import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';

@Component({
  selector: 'app-chat-shell',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './chat-shell.component.html',
  styleUrl: './chat-shell.component.scss'
})
export class ChatShellComponent {}
