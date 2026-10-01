import { Component } from '@angular/core';

import { AxMascot } from '../../components/ax-mascot/ax-mascot';
import { TranslatePipe } from '../../i18n/translate.pipe';

interface Project {
  name: string;
  description: string;
  stack: string[];
  url: string;
  kind: 'web' | 'hardware' | 'java' | 'data';
}

interface Milestone {
  period: string;
  title: string;
  detail: string;
}

@Component({
  selector: 'app-about',
  imports: [AxMascot, TranslatePipe],
  templateUrl: './about.html',
  styleUrl: './about.scss'
})
export class About {
  readonly year = new Date().getFullYear();

  readonly githubUrl = 'https://github.com/bogdansusman-prog';
  readonly linkedinUrl = 'https://www.linkedin.com/in/bogdan-susman/';

  readonly stack = [
    'C#', 'ASP.NET Core', 'Entity Framework Core', 'PostgreSQL', 'ASP.NET Identity + JWT',
    'Angular', 'TypeScript', 'Signals', 'Chart.js', 'SCSS', 'SVG + CSS animations', 'xUnit'
  ];

  readonly highlights = [
    { title: 'Conturi separate', text: 'Fiecare utilizator își vede doar datele lui, cu filtre globale în EF Core și autentificare JWT.' },
    { title: 'A meritat?', text: 'Notezi cheltuielile după 7 zile și afli unde îți pierzi, de fapt, banii.' },
    { title: 'Detectiv de abonamente', text: 'Găsește singur plățile recurente și te anunță când ceva s-a scumpit pe ascuns.' },
    { title: 'Prognoza Monte Carlo', text: '2.000 de simulări pe istoricul tău real arată unde va fi soldul peste 30–90 de zile.' },
    { title: 'Adăugare în limbaj natural', text: '„ieri 45 lei pizza cu Andrei” devine o tranzacție completă, offline.' },
    { title: 'Ax, consilierul', text: 'Un scor de sănătate financiară explicabil, cu 3 pași concreți și un chat care răspunde cu cifrele tale.' }
  ];

  readonly projects: Project[] = [
    {
      name: 'OwlBank',
      description: 'Aplicație de digital banking făcută în echipă: frontend complet în Angular (autentificare JWT, transferuri, carduri, extrase) și o versiune mobilă în Flutter.',
      stack: ['Angular', 'TypeScript', 'Angular Material', 'Flutter', 'Dart'],
      url: 'https://github.com/bogdansusman-prog/OwlBank-Fe',
      kind: 'web'
    },
    {
      name: 'MIPS32 Pipeline',
      description: 'Procesor MIPS pe 32 de biți cu pipeline în 5 etaje, scris în VHDL și rulat pe placa FPGA Nexys A7.',
      stack: ['VHDL', 'Vivado', 'FPGA'],
      url: 'https://github.com/bogdansusman-prog/mips32-pipeline-vhdl',
      kind: 'hardware'
    },
    {
      name: 'FIFO pe FPGA',
      description: 'Memorie FIFO 16×16 cu pointeri circulari, afișaj pe 7 segmente și butoane debounced, pe placa Basys 3. Are și demo video.',
      stack: ['VHDL', 'Basys 3', 'Digital design'],
      url: 'https://github.com/bogdansusman-prog/fifo-buffer-vhdl-basys3',
      kind: 'hardware'
    },
    {
      name: 'Queue Simulator',
      description: 'Simulator de cozi multithreaded: fiecare casă rulează pe propriul thread, cu strategii de distribuire și animație în timp real.',
      stack: ['Java', 'Multithreading', 'Swing'],
      url: 'https://github.com/bogdansusman-prog/queue-simulator-java',
      kind: 'java'
    },
    {
      name: 'Orders Management',
      description: 'Gestiune de comenzi pe PostgreSQL, cu arhitectură stratificată și un DAO generic construit cu reflection.',
      stack: ['Java', 'PostgreSQL', 'JDBC', 'Reflection'],
      url: 'https://github.com/bogdansusman-prog/orders-management-java',
      kind: 'java'
    },
    {
      name: 'Library Database',
      description: 'Bază de date pentru o bibliotecă: schemă normalizată cu relații 1:1 și M:N și 16 interogări SQL.',
      stack: ['PostgreSQL', 'SQL', 'Database design'],
      url: 'https://github.com/bogdansusman-prog/library-database-postgresql',
      kind: 'data'
    }
  ];

  readonly milestones: Milestone[] = [
    {
      period: '2020 – 2024',
      title: 'Liceul Teoretic „Aurel Lazăr”, Oradea',
      detail: 'Profil matematică-informatică. Primele programe, prima pasiune pentru logică și un proiect Erasmus+ în Portugalia.'
    },
    {
      period: '2024 – prezent',
      title: 'Universitatea Tehnică din Cluj-Napoca',
      detail: 'Calculatoare și Tehnologia Informației: de la Java, OOP și baze de date până la procesoare MIPS și FPGA.'
    },
    {
      period: 'Vara 2026',
      title: 'Full-stack pe cont propriu',
      detail: 'Angular, C#, ASP.NET Core, Flutter. Am construit OwlBank în echipă și apoi Ax Tracker de la zero.'
    },
    {
      period: 'Următorul pas',
      title: 'Primul internship',
      detail: 'Caut un loc unde să contribui la produse reale și să cresc ca inginer software. Ax Tracker e dovada că pot duce un proiect de la idee la produs.'
    }
  ];
}
