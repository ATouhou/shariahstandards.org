import { Component, OnInit,ChangeDetectorRef } from '@angular/core';
import { TermComponent } from '../term/term.component';
import { Router, ActivatedRoute }       from '@angular/router';
import { QuranReferenceComponent } from '../quran-reference/quran-reference.component';
import { ShurahService } from '../shurah.service';

@Component({
  selector: 'app-standards',
  templateUrl: './standards.component.html',
  styleUrls: ['./standards.component.css'],
  providers:[ShurahService]
  // directives:[TermComponent,QuranReferenceComponent]
})
export class StandardsComponent implements OnInit {

  constructor(   
    private route:ActivatedRoute,
    private router:Router,
    private changeDetectorRef:ChangeDetectorRef,
    private shurahService:ShurahService
 ) { }
  activeSection:string;
  collapsedSections:string[]=["website-management","prayer-times-rules","zakat-rules","inheritance-rules","decision-making-rules"]
//  collapsedSections:string[]=["website-management","prayer-times-rules","zakat-rules",]
  ngOnInit() {
     this.getRouteParamsSubscribe=this.route.params.subscribe(params=>{
        if(params['section']){
          this.activeSection=params['section'];
          //this.toggleCollapse(params['section']);
        }
        this.changeDetectorRef.detectChanges();
     });
     // this.shurahService.getRootOrganisation().subscribe(result=>{
     //   this.rootOrganisation=result.json();
     //   this.changeDetectorRef.detectChanges();
     //  // console.log(JSON.stringify(this.rootOrganisation));
     // })
  }
  rootOrganisation:{}
  toggleCollapse(sectionName:string){
    if(this.activeSection!=sectionName){
      // this.activeSection = sectionName;
      this.router.navigate(["/standards/"+sectionName]);
    }else{
      // this.activeSection=null;
      this.router.navigate(["/standards"]);
    }
  	// var index=this.collapsedSections.indexOf(sectionName);
  	// if(index>=0){
  	// 	this.collapsedSections.splice(index,1);
  	// }else{
  	// 	this.collapsedSections.push(sectionName);
   //    this.router.navigate(["/standards/"+sectionName]);
  	// }
  }
  private getRouteParamsSubscribe:any;
  ngOnDestroy() {
    this.getRouteParamsSubscribe.unsubscribe();
  }
  expanded(sectionName:string){
      return this.activeSection==sectionName;
  //	var index=this.collapsedSections.indexOf(sectionName);
 	//return index<0;
  }
}
